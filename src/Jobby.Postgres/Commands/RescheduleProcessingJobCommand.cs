using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class RescheduleProcessingJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public RescheduleProcessingJobCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _commandText = @$"
            UPDATE {TableName.Jobs(settings)}
            SET
                status = {(int)JobStatus.Scheduled},
                last_finished_at = $1,
                scheduled_start_at = $2,
                error = $3
            WHERE 
                id = $4
                AND status = {(int)JobStatus.Processing}
                AND server_id = $5
        ";
    }

    public async Task ExecuteAsync(ProcessingJob job, DateTime scheduledStartTime, string? error)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = DateTime.UtcNow },
                new() { Value = scheduledStartTime },
                new() { Value = error as object ?? DBNull.Value },
                new() { Value = job.JobId },
                new() { Value = job.ServerId }
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
