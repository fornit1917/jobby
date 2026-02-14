using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class TakeBatchToProcessingCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;
    private readonly string _commandForDisabledSerializableGroups;

    public TakeBatchToProcessingCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _commandText = $@"SELECT * FROM {DbName.TakeToProcessing(settings)}($1, $2, $3)";

        _commandForDisabledSerializableGroups = $@"
            WITH
                ready_jobs AS (
	                SELECT id FROM {DbName.Jobs(settings)} 
	                WHERE
                        queue_name = $1
                        AND status = {(int)JobStatus.Scheduled}
                        AND scheduled_start_at <= now()
	                ORDER BY scheduled_start_at
	                LIMIT $2
	                FOR UPDATE SKIP LOCKED
                ),
                updated AS (
                    UPDATE {DbName.Jobs(settings)}
                    SET
	                    status = {(int)JobStatus.Processing},
	                    last_started_at = now(),
	                    started_count = started_count + 1,
                        server_id = $3
                    WHERE id IN (SELECT id FROM ready_jobs)
                    RETURNING id, job_name, job_param, started_count, cron, next_job_id, scheduled_start_at, server_id
                )
            SELECT id, job_name, job_param, started_count, cron, next_job_id, scheduled_start_at, server_id
            FROM updated
        ";
    }

    public async Task ExecuteAndWriteToListAsync(GetJobsRequest request, List<JobExecutionModel> result)
    {
        result.Clear();

        var commandText = request.DisableSerializableGroups ? _commandForDisabledSerializableGroups : _commandText;
        await using var conn = await _dataSource.OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(commandText, conn);
        cmd.Parameters.Add(new() { Value = request.QueueName });  // 1
        cmd.Parameters.Add(new() { Value = request.BatchSize });  // 2
        cmd.Parameters.Add(new() { Value = request.ServerId });   // 3

        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (true)
        {
            var job = await reader.GetJobAsync();
            if (job == null)
            {
                break;
            }

            result.Add(job);
        }
        
        result.Sort((a, b) => a.ScheduledStartAt.CompareTo(b.ScheduledStartAt));
    }
}
