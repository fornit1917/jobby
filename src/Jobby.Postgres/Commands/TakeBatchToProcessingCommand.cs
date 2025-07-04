﻿using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class TakeBatchToProcessingCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public TakeBatchToProcessingCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _commandText = $@"
            WITH
                ready_jobs AS (
	                SELECT id FROM {TableName.Jobs(settings)} 
	                WHERE
                        status = {(int)JobStatus.Scheduled}
                        AND scheduled_start_at <= $1
	                ORDER BY scheduled_start_at
	                LIMIT $2
	                FOR UPDATE SKIP LOCKED
                ),
                updated AS (
                    UPDATE {TableName.Jobs(settings)}
                    SET
	                    status = {(int)JobStatus.Processing},
	                    last_started_at = $1,
	                    started_count = started_count + 1,
                        server_id = $3
                    WHERE id IN (SELECT id FROM ready_jobs)
                    RETURNING id, job_name, job_param, started_count, cron, next_job_id, scheduled_start_at
                )
            SELECT id, job_name, job_param, started_count, cron, next_job_id, scheduled_start_at
            FROM updated
            ORDER BY scheduled_start_at
        ";
    }

    public async Task ExecuteAndWriteToListAsync(string serverId, DateTime now, int maxBatchSize, List<JobExecutionModel> result)
    {
        result.Clear();

        await using var conn = await _dataSource.OpenConnectionAsync();

        await using var cmd = new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = now },              // 1
                new() { Value = maxBatchSize },     // 2
                new() { Value = serverId }          // 3
            }
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (true)
        {
            var job = await reader.GetJobAsync();
            if (job == null)
            {
                return;
            }

            result.Add(job);
        }
    }
}
