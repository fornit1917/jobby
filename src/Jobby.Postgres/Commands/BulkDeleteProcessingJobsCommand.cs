using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkDeleteProcessingJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;
    private readonly string _deleteAndUnlockNextCommandText;
    private readonly string _deleteAndUnlockSequenceCommandText;

    public BulkDeleteProcessingJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _deleteCommandText = @$"
            DELETE FROM {TableName.Jobs(settings)}
            WHERE
                id = ANY($1)
                AND status = {(int)JobStatus.Processing}
                AND server_id = $2
        ";

        _deleteAndUnlockNextCommandText = $@"
            WITH complete_and_get_next_job_id AS (
	            DELETE FROM {TableName.Jobs(settings)}
	            WHERE
		            id = ANY($1)
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $2
	            RETURNING next_job_id
            )
            UPDATE {TableName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = ANY($3)
                AND status = {(int)JobStatus.WaitingPrev}
        ";

        _deleteAndUnlockSequenceCommandText = @$"
            WITH deleted AS (
                DELETE FROM {TableName.Jobs(settings)}
                WHERE
                    id = ANY($1)
                    AND status = {(int)JobStatus.Processing}
                    AND server_id = $2
                RETURNING sequence_id
            ),
            sequence_next AS (
                SELECT DISTINCT ON (sequence_id) id, sequence_id
                FROM {TableName.Jobs(settings)}
                WHERE sequence_id IN (SELECT sequence_id FROM deleted WHERE sequence_id IS NOT NULL)
                  AND status = {(int)JobStatus.WaitingPrev}
                ORDER BY sequence_id, scheduled_start_at ASC
            )
            UPDATE {TableName.Jobs(settings)} j
            SET status = {(int)JobStatus.Scheduled}
            FROM sequence_next sn
            WHERE j.id = sn.id
        ";
    }

    public async Task ExecuteAsync(ProcessingJobsList jobs, IReadOnlyList<Guid>? nextJobIds = null, IReadOnlyList<string>? sequenceIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // IMPORTANT: Check Count > 0, not just != null, so empty lists use non-sequence path
        if (sequenceIds is { Count: > 0 })
        {
            await using var cmd = new NpgsqlCommand(_deleteAndUnlockSequenceCommandText, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            cmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });
            await cmd.ExecuteNonQueryAsync();
            return;
        }

        if (nextJobIds is { Count: > 0 })
        {
            await using var deleteAndUnlockNextCmd = new NpgsqlCommand(_deleteAndUnlockNextCommandText, conn);
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });
            deleteAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = nextJobIds });

            await deleteAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn);
            deleteCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            deleteCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });

            await deleteCmd.ExecuteNonQueryAsync();
        }
    }
}
