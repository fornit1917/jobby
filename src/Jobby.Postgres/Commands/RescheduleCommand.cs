using Jobby.Abstractions.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class RescheduleCommand
{
    private static readonly string CommandText = @$"
        UPDATE jobby_jobs
        SET
            status = {(int)JobStatus.Scheduled},
            last_finished_at = @last_finished_at,
            scheduled_start_at = @scheduled_start_at
        WHERE id = @id;
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, long jobId, DateTime scheduledStartTime)
    {
        await using var cmd = new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("last_finished_at", DateTime.UtcNow),
                new("scheduled_start_at", scheduledStartTime),
                new("id", jobId)
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
