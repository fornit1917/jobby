using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkCompleteJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _updateStatusCommandText;
    private readonly string _scheduleNextJobCommandText;

    public BulkCompleteJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _updateStatusCommandText = @$"
            UPDATE {TableName.Jobs(settings)}
            SET
                status = {(int)JobStatus.Completed},
                error = null,
                last_finished_at = $1
            WHERE id = ANY($2);
        ";

        _scheduleNextJobCommandText = @$"
            UPDATE {TableName.Jobs(settings)} SET status={(int)JobStatus.Scheduled} WHERE id = ANY($1)
        ";
    }

    public async Task ExecuteAsync(IReadOnlyList<Guid> jobIds, IReadOnlyList<Guid>? nextJobIds = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        if (nextJobIds is { Count: > 0 })
        {
            await using var batch = new NpgsqlBatch(conn);

            var updateCmd = new NpgsqlBatchCommand(_updateStatusCommandText)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobIds }
                }
            };

            var scheduleNextCmd = new NpgsqlBatchCommand(_scheduleNextJobCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobIds }
                }
            };

            batch.BatchCommands.Add(updateCmd);
            batch.BatchCommands.Add(scheduleNextCmd);

            await batch.ExecuteNonQueryAsync();
        }
        else
        {
            await using var updateCmd = new NpgsqlCommand(_updateStatusCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = DateTime.UtcNow },
                    new() { Value = jobIds }
                }
            };

            await updateCmd.ExecuteNonQueryAsync();
        }
    }
}
