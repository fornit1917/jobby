using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal static class DeleteJobCommand
{
    private const string DeleteJobCommandText = @"DELETE FROM jobby_jobs WHERE id = @id";

    // todo: use batch command
    // todo: use positional params instead of named here and in other commands
    private static readonly string DeleteAndScheduleNextJobCommandText = @$"
        DELETE FROM jobby_jobs WHERE id = @id;
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = @next_job_id;
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, Guid jobId, Guid? nextJobId = null)
    {
        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(DeleteJobCommandText, conn)
            {
                Parameters =
                {
                    new("id", jobId)
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new NpgsqlCommand(DeleteAndScheduleNextJobCommandText, conn)
            {
                Parameters =
                {
                    new("id", jobId),
                    new("next_job_id", nextJobId.Value)
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
