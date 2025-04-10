using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class RescheduleCommand
{
    private static readonly string CommandText = @$"
        UPDATE jobby_jobs
        SET
            status = {(int)JobStatus.Scheduled},
            last_finished_at = $1,
            scheduled_start_at = $2
        WHERE id = $3;
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, DateTime scheduledStartTime)
    {
        await using var cmd = new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new() { Value = DateTime.UtcNow },
                new() { Value = scheduledStartTime },
                new() { Value = jobId }
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
