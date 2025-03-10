using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class UpdateStatusCommand
{
    private const string UpdateCommandText = @"
        UPDATE jobby_jobs
        SET
            status = @status,
            last_finished_at = @last_finished_at
        WHERE id = @id;
    ";

    private static readonly string UpdateAndScheduleNextCommandText = @$"
        UPDATE jobby_jobs
        SET
            status = @status,
            last_finished_at = @last_finished_at
        WHERE id = @id;
        
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = @next_job_id;
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, JobStatus newStatus, Guid? nextJobId = null)
    {
        var finishedAt = DateTime.UtcNow;
        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(UpdateCommandText, conn)
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
        else
        {
            await using var cmd = new NpgsqlCommand(UpdateAndScheduleNextCommandText, conn)
            {
                Parameters =
                {
                    new("status", (int)newStatus),
                    new("last_finished_at", finishedAt),
                    new("id", jobId),
                    new("next_job_id", nextJobId.Value)
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
