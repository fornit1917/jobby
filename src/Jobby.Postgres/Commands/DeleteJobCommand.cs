using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class DeleteJobCommand
{
    private const string DeleteJobCommandText = @"DELETE FROM jobby_jobs WHERE id = $1";

    private static readonly string ScheduleNextJobCommandText = @$"
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = $1
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, Guid? nextJobId = null)
    {
        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(DeleteJobCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = jobId }
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var batch = new NpgsqlBatch(conn);

            var deleteCmd = new NpgsqlBatchCommand(DeleteJobCommandText)
            {
                Parameters =
                {
                    new() { Value = jobId }
                }
            };

            var scheduleNextCmd = new NpgsqlBatchCommand(ScheduleNextJobCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobId.Value }
                }
            };

            batch.BatchCommands.Add(deleteCmd);
            batch.BatchCommands.Add(scheduleNextCmd);

            await batch.ExecuteNonQueryAsync();
        }
    }
}
