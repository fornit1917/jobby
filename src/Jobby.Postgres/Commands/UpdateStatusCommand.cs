using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class UpdateStatusCommand
{
    private readonly NpgsqlDataSource _dataSource;

    private readonly string _updateStatusCommandText;
    private readonly string _scheduleNextJobCommandText;

    public UpdateStatusCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _updateStatusCommandText = $@"
            UPDATE {TableName.Jobs(settings)}
            SET
                status = $1,
                last_finished_at = $2
            WHERE id = $3;
        ";

        _scheduleNextJobCommandText = @$"
            UPDATE {TableName.Jobs(settings)} SET status={(int)JobStatus.Scheduled} WHERE id = $1
        ";
    }

    public async Task ExecuteAsync(Guid jobId, JobStatus newStatus, Guid? nextJobId = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var finishedAt = DateTime.UtcNow;
        if (nextJobId == null)
        {
            await using var cmd = new NpgsqlCommand(_updateStatusCommandText, conn)
            {
                Parameters =
                {
                    new() { Value = (int)newStatus },   // 1
                    new() { Value = finishedAt },       // 2
                    new() { Value = jobId }             // 3
                }
            };
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var batch = new NpgsqlBatch(conn);

            var updateCmd = new NpgsqlBatchCommand(_updateStatusCommandText)
            {
                Parameters =
                {
                    new() { Value = (int)newStatus },   // 1
                    new() { Value = finishedAt },       // 2
                    new() { Value = jobId }             // 3
                }
            };

            var scheduleNextCommand = new NpgsqlBatchCommand(_scheduleNextJobCommandText)
            {
                Parameters =
                {
                    new() { Value = nextJobId.Value }
                }
            };

            batch.BatchCommands.Add(updateCmd);
            batch.BatchCommands.Add(scheduleNextCommand);

            await batch.ExecuteNonQueryAsync();
        }
    }
}
