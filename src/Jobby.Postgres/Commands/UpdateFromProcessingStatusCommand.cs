using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class UpdateFromProcessingStatusCommand
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _updateStatusCommandText;
    private readonly string _updateAndUnlockNextCommandText;
    private readonly string _updateAndUnlockSequenceCommandText;

    public UpdateFromProcessingStatusCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _updateStatusCommandText = $@"
            UPDATE {TableName.Jobs(settings)}
            SET
                status = $1,
                last_finished_at = $2,
                error = $3
            WHERE
                id = $4
                AND status = {(int)JobStatus.Processing}
                AND server_id = $5
        ";

        _updateAndUnlockNextCommandText = @$"
            WITH complete_and_get_next_job_id AS (
	            UPDATE {TableName.Jobs(settings)}
	            SET
		            status = $1,
		            last_finished_at = $2,
		            error = $3
	            WHERE
		            id = $4
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $5
	            RETURNING next_job_id
            )
            UPDATE {TableName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = $6
                AND status = {(int)JobStatus.WaitingPrev}
        ";

        _updateAndUnlockSequenceCommandText = @$"
            WITH updated AS (
                UPDATE {TableName.Jobs(settings)}
                SET
                    status = $1,
                    last_finished_at = $2,
                    error = $3
                WHERE
                    id = $4
                    AND status = {(int)JobStatus.Processing}
                    AND server_id = $5
                RETURNING sequence_id
            ),
            sequence_next AS (
                SELECT id FROM {TableName.Jobs(settings)}
                WHERE sequence_id IN (SELECT sequence_id FROM updated WHERE sequence_id IS NOT NULL)
                  AND status = {(int)JobStatus.WaitingPrev}
                ORDER BY scheduled_start_at ASC
                LIMIT 1
            )
            UPDATE {TableName.Jobs(settings)}
            SET status = {(int)JobStatus.Scheduled}
            WHERE id IN (SELECT id FROM sequence_next)
        ";
    }

    public async Task ExecuteAsync(ProcessingJob job, JobStatus newStatus, string? error, Guid? nextJobId, string? sequenceId)
    {
        // Validate mutual exclusivity
        if (sequenceId != null && nextJobId != null)
        {
            throw new InvalidOperationException(
                "Job cannot have both sequenceId and nextJobId set. These are mutually exclusive sequencing mechanisms.");
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        var finishedAt = DateTime.UtcNow;

        if (sequenceId != null)
        {
            await using var cmd = new NpgsqlCommand(_updateAndUnlockSequenceCommandText, conn);
            cmd.Parameters.Add(new  NpgsqlParameter { Value = (int)newStatus });                  // $1
            cmd.Parameters.Add(new NpgsqlParameter { Value = finishedAt });                       // $2
            cmd.Parameters.Add(new NpgsqlParameter { Value = error as object ?? DBNull.Value });  // $3
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });                        // $4
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });                     // $5
            await cmd.ExecuteNonQueryAsync();
            return;
        }

        if (nextJobId == null || newStatus != JobStatus.Completed)
        {
            await using var cmd = new NpgsqlCommand(_updateStatusCommandText, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = (int)newStatus });                   // $1
            cmd.Parameters.Add(new NpgsqlParameter { Value = finishedAt });                       // $2
            cmd.Parameters.Add(new NpgsqlParameter { Value = error as object ?? DBNull.Value });  // $3
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });                        // $4
            cmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });                     // $5
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateAndUnlockNextCmd = new NpgsqlCommand(_updateAndUnlockNextCommandText, conn);
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = (int)newStatus });                  // $1
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = finishedAt });                      // $2
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = error as object ?? DBNull.Value }); // $3
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = job.JobId });                       // $4
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = job.ServerId });                    // $5
            updateAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = nextJobId });                       // $6
            await updateAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
    }
}
