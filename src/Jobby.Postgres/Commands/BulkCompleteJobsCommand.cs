using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class BulkCompleteJobsCommand
{
    private static readonly string UpdateStatusCommandText = @$"
        UPDATE jobby_jobs
        SET
            status = {(int)JobStatus.Completed},
            last_finished_at = $1
        WHERE id = ANY($2);
    ";

    private static readonly string ScheduleNextJobCommandText = @$"
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = ANY($1)
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, 
        IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        if (nextJobIds is { Count: > 0 })
        {
            await using var batch = new NpgsqlBatch(conn);

            var updateCmd = new NpgsqlBatchCommand(UpdateStatusCommandText)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobIds }
                }
            };

            var scheduleNextCmd = new NpgsqlBatchCommand(ScheduleNextJobCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobIds }
                }
            };

            batch.BatchCommands.Add(updateCmd);
            batch.BatchCommands.Add(scheduleNextCmd);

            await batch.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateCmd = new NpgsqlCommand(UpdateStatusCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobIds }
                }
            };

            await updateCmd.ExecuteNonQueryAsync();
        }
    }
}
