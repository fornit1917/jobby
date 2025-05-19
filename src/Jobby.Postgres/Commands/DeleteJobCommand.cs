using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class DeleteJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _deleteJobCommandText;
    private readonly string _scheduleNextJobCommandText;

    public DeleteJobCommand(NpgsqlDataSource dataSource, PgStorageSettings settings)
    {
        _dataSource = dataSource;
        
        _deleteJobCommandText = $"DELETE FROM {TableName.Jobs(settings)} WHERE id = $1";
        
        _scheduleNextJobCommandText = @$"
            UPDATE {TableName.Jobs(settings)} SET status={(int)JobStatus.Scheduled} WHERE id = $1
        ";
    }

    public async Task ExecuteAsync(Guid jobId, Guid? nextJobId = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(_deleteJobCommandText, conn)
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

            var deleteCmd = new NpgsqlBatchCommand(_deleteJobCommandText)
            {
                Parameters =
                {
                    new() { Value = jobId }
                }
            };

            var scheduleNextCmd = new NpgsqlBatchCommand(_scheduleNextJobCommandText)
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
