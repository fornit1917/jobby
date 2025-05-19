using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkDeleteJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteCommandText;
    private readonly string _scheduleNextJobsCommandText;

    public BulkDeleteJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _deleteCommandText = $"DELETE FROM {TableName.Jobs(settings)} WHERE id = ANY($1)";
        
        _scheduleNextJobsCommandText = @$"
            UPDATE {TableName.Jobs(settings)} SET status={(int)JobStatus.Scheduled} WHERE id = ANY($1)
        ";
    }

    public async Task ExecuteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        if (nextJobIds is { Count : > 0})
        {
            await using var batch = new NpgsqlBatch(conn);
            
            var deleteCmd = new NpgsqlBatchCommand(_deleteCommandText)
            {
                Parameters =
                {
                    new() { Value = jobIds }
                },
            };
            
            var scheduleNextCmd = new NpgsqlBatchCommand(_scheduleNextJobsCommandText)
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
            await using var deleteCmd = new NpgsqlCommand(_deleteCommandText, conn)
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
