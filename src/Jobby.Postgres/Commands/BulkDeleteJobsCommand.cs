using Jobby.Core.Models;
using Npgsql;

namespace Jobby.Postgres.Commands;

public class BulkDeleteJobsCommand
{
    private const string DeleteCommandText = @"DELETE FROM jobby_jobs WHERE id = ANY($1)";
    
    private static readonly string ScheduleNextJobsCommandText = @$"
        UPDATE jobby_jobs SET status={(int)JobStatus.Scheduled} WHERE id = ANY($1)
    ";

    public static async Task ExecuteAsync(NpgsqlConnection conn, IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        if (nextJobIds is { Count : > 0})
        {
            var batch = new NpgsqlBatch(conn);
            
            var deleteCmd = new NpgsqlBatchCommand(DeleteCommandText)
            {
                Parameters =
                {
                    new() { Value = jobIds }
                },
            };
            
            var scheduleNextCmd = new NpgsqlBatchCommand(ScheduleNextJobsCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobIds }
                }
            };

            batch.BatchCommands.Add(deleteCmd);
            batch.BatchCommands.Add(scheduleNextCmd);

            await batch.ExecuteNonQueryAsync();
        }
        else
        {
            var deleteCmd = new NpgsqlCommand(DeleteCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = jobIds }
                }
            };

            await deleteCmd.ExecuteNonQueryAsync();
        }
    }
}
