using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkCompleteProcessingJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _completeCommandText;
    private readonly string _completeAndUnlockNextCommandText;
    private readonly string _completeAndUnlockSequenceCommandText;

    public BulkCompleteProcessingJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _completeCommandText = @$"
            UPDATE {TableName.Jobs(settings)}
            SET
                status = {(int)JobStatus.Completed},
                error = null,
                last_finished_at = $1
            WHERE
                id = ANY($2)
                AND status = {(int)JobStatus.Processing}
                AND server_id = $3
        ";

        _completeAndUnlockNextCommandText = @$"
            WITH complete_and_get_next_job_id AS (
	            UPDATE {TableName.Jobs(settings)}
	            SET
		            status = {(int)JobStatus.Completed},
		            last_finished_at = $1,
		            error = NULL
	            WHERE
		            id = ANY($2)
		            AND status = {(int)JobStatus.Processing}
		            AND server_id = $3
	            RETURNING next_job_id
            )
            UPDATE {TableName.Jobs(settings)}
            SET
	            status = {(int)JobStatus.Scheduled}
            WHERE
                id IN (SELECT next_job_id FROM complete_and_get_next_job_id)
                AND id = ANY($4)
                AND status = {(int)JobStatus.WaitingPrev}
        ";

        _completeAndUnlockSequenceCommandText = @$"
            WITH updated AS (
                UPDATE {TableName.Jobs(settings)}
                SET
                    status = {(int)JobStatus.Completed},
                    error = null,
                    last_finished_at = $1
                WHERE
                    id = ANY($2)
                    AND status = {(int)JobStatus.Processing}
                    AND server_id = $3
                RETURNING sequence_id
            ),
            sequence_next AS (
                SELECT DISTINCT ON (sequence_id) id, sequence_id
                FROM {TableName.Jobs(settings)}
                WHERE sequence_id IN (SELECT sequence_id FROM updated WHERE sequence_id IS NOT NULL)
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
            await using var cmd = new NpgsqlCommand(_completeAndUnlockSequenceCommandText, conn);
            cmd.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            cmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });
            await cmd.ExecuteNonQueryAsync();
            return;
        }

        if (nextJobIds is { Count: > 0 })
        {
            await using var completeAndUnlockNextCmd = new NpgsqlCommand(_completeAndUnlockNextCommandText, conn);
            completeAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
            completeAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            completeAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });
            completeAndUnlockNextCmd.Parameters.Add(new NpgsqlParameter { Value = nextJobIds });

            await completeAndUnlockNextCmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateCmd = new NpgsqlCommand(_completeCommandText, conn);
            updateCmd.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
            updateCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.JobIds });
            updateCmd.Parameters.Add(new NpgsqlParameter { Value = jobs.ServerId });

            await updateCmd.ExecuteNonQueryAsync();
        }
    }
}
