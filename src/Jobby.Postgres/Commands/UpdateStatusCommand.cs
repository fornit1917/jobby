using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class UpdateStatusCommand
{
    private const string CommandText = @"
        UPDATE jobby_jobs
        SET
            status = @status,
            last_finished_at = @last_finished_at
        WHERE id = @id;
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, JobStatus newStatus)
    {
        var finishedAt = DateTime.UtcNow;
        await using var cmd = new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("status", (int)newStatus),
                new("last_finished_at", finishedAt),
                new("id", jobId)
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
