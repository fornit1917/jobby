using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class DeleteProcessingJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;
    private readonly string _deleteAndUnlockNextCommandText;
    private readonly string _deleteAndUnlockSequenceCommandText;

    public DeleteProcessingJobCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _deleteCommandText = @$"
            DELETE FROM {TableName.Jobs(settings)}
            WHERE
                id = $1
                AND status = {(int)JobStatus.Processing}
                AND server_id = $2";

        _deleteAndUnlockNextCommandText = $@"
            WITH complete_and_get_next_job_id AS (
	            DELETE FROM {TableName.Jobs(settings)}
	            WHERE
		            id = $1
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $2
	            RETURNING next_job_id
            )
            UPDATE {TableName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = $3
                AND status = {(int)JobStatus.WaitingPrev}
        ";

        _deleteAndUnlockSequenceCommandText = @$"
            WITH deleted AS (
                DELETE FROM {TableName.Jobs(settings)}
                WHERE
                    id = $1
                    AND status = {(int)JobStatus.Processing}
                    AND server_id = $2
                RETURNING sequence_id
            ),
            sequence_next AS (
                SELECT id FROM {TableName.Jobs(settings)}
                WHERE sequence_id IN (SELECT sequence_id FROM deleted WHERE sequence_id IS NOT NULL)
                  AND status = {(int)JobStatus.WaitingPrev}
                ORDER BY scheduled_start_at ASC
                LIMIT 1
            )
            UPDATE {TableName.Jobs(settings)}
            SET status = {(int)JobStatus.Scheduled}
            WHERE id IN (SELECT id FROM sequence_next)
        ";
    }

    public async Task ExecuteAsync(ProcessingJob job, Guid? nextJobId = null, string? sequenceId = null)
    {
        // Validate mutual exclusivity
        if (nextJobId != null && sequenceId != null)
        {
            throw new InvalidOperationException(
                "Job cannot have both nextJobId and sequenceId set. These are mutually exclusive sequencing mechanisms.");
        }

        await using var conn = await _dataSource.OpenConnectionAsync();

        if (sequenceId != null)
        {
            await using var cmd = new NpgsqlCommand(_deleteAndUnlockSequenceCommandText, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });
            await cmd.ExecuteNonQueryAsync();
            return;
        }

        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(_deleteCommandText, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var deleteAndUnlockNextCmd = new NpgsqlCommand(_deleteAndUnlockNextCommandText, conn);
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = nextJobId });

            await deleteAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
    }
}
