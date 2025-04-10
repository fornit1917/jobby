using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class UpdateStatusCommand
{
    private const string UpdateStatusCommandText = @"
        UPDATE jobby_jobs
        SET
            status = $1,
            last_finished_at = $2
        WHERE id = $3;
    ";

    private static readonly string ScheduleNextJobCommandText = @$"
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = $1
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, JobStatus newStatus, Guid? nextJobId = null)
    {
        var finishedAt = DateTime.UtcNow;
        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(UpdateStatusCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = (int)newStatus },   // 1
                    new() { Value = finishedAt },       // 2
                    new() { Value = jobId }             // 3
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var batch = new NpgsqlBatch(conn);

            var updateCmd = new NpgsqlBatchCommand(UpdateStatusCommandText)
            {
                Parameters =
                {
                    new() { Value = (int)newStatus },   // 1
                    new() { Value = finishedAt },       // 2
                    new() { Value = jobId }             // 3
                }
            };

            var scheduleNextCommand = new NpgsqlBatchCommand(ScheduleNextJobCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobId.Value }
                }
            };

            batch.BatchCommands.Add(updateCmd);
            batch.BatchCommands.Add(scheduleNextCommand);

            await batch.ExecuteNonQueryAsync();
        }
    }
}
