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
            UPDATE {DbName.Jobs(settings)}
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

    public async Task ExecuteAsync(JobExecutionModel job, DateTime scheduledStartTime, string? error)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(_commandText, conn);
        cmd.Parameters.Add(new() { Value = DateTime.UtcNow });
        cmd.Parameters.Add(new() { Value = scheduledStartTime });
        cmd.Parameters.Add(new() { Value = error as object ?? DBNull.Value });
        cmd.Parameters.Add(new() { Value = job.Id });
        cmd.Parameters.Add(new() { Value = job.ServerId });
        await cmd.ExecuteNonQueryAsync();
    }
}
