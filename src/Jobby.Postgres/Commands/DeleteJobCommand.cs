using Npgsql;

namespace Jobby.Postgres.Commands;

internal class DeleteJobCommand
{
    private const string CommandText = @"DELETE FROM jobby_jobs WHERE id = @id";

    public static async Task ExecuteAsync(NpgsqlConnection conn, long jobId)
    {
        await using var cmd = new NpgsqlCommand(CommandText, conn)
        {
            Parameters =
            {
                new("id", jobId)
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
