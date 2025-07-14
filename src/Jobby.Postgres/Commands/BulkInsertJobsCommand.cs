using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkInsertJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public BulkInsertJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _commandText = @$"
            INSERT INTO {TableName.Jobs(settings)} (
                id,
                job_name,
                job_param,
                status,
                created_at,
                scheduled_start_at,
                next_job_id,
                can_be_restarted,
                cron
            )
            VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8,
                $9
            )
        ";
    }

    public async Task ExecuteAsync(IReadOnlyList<JobCreationModel> jobs)
    {
        if (jobs is not { Count: > 0 })
            return;

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var batch = new NpgsqlBatch(conn);
        PrepareCommand(batch, jobs);
        await batch.ExecuteNonQueryAsync();
    }

    public void Execute(IReadOnlyList<JobCreationModel> jobs)
    {
        if (jobs is not { Count: > 0 })
            return;

        using var conn = _dataSource.OpenConnection();
        using var batch = new NpgsqlBatch(conn);
        PrepareCommand(batch, jobs);
        batch.ExecuteNonQuery();
    }

    private void PrepareCommand(NpgsqlBatch batch, IReadOnlyList<JobCreationModel> jobs)
    {
        foreach (var job in jobs)
        {
            var cmd = new NpgsqlBatchCommand(_commandText)
            {
                Parameters =
                {
                    new() { Value = job.Id },
                    new() { Value = job.JobName },
                    new() { Value = (object?)job.JobParam ?? DBNull.Value },
                    new() { Value = (int)job.Status },
                    new() { Value = job.CreatedAt },
                    new() { Value = job.ScheduledStartAt },
                    new() { Value = (object?)job.NextJobId ?? DBNull.Value },
                    new() { Value = job.CanBeRestarted },
                    new() { Value = (object?)job.Cron ?? DBNull.Value },
                }
            };
            batch.BatchCommands.Add(cmd);
        }
    }
}
