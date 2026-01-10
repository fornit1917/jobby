using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class BulkInsertJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;
    private readonly string _commandWithSequenceIdText;

    public BulkInsertJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        var blockSequenceOnFailure = settings.SequenceFailureBehavior == SequenceFailureBehavior.Block;

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

        _commandWithSequenceIdText = blockSequenceOnFailure
            ? @$"
                WITH try_ins AS (
                    INSERT INTO {TableName.Jobs(settings)} (
                        id, job_name, job_param, status, created_at, scheduled_start_at, can_be_restarted, sequence_id
                    )
                    SELECT $1, $2, $3, $4, $5, $6, $7, $8
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM {TableName.Jobs(settings)}
                        WHERE sequence_id = $8
                          AND (status = {(int)JobStatus.WaitingPrev} OR status = {(int)JobStatus.Failed})
                    )
                    ON CONFLICT (sequence_id) WHERE (status = {(int)JobStatus.Scheduled}) DO NOTHING
                    RETURNING 1 AS inserted
                )
                INSERT INTO {TableName.Jobs(settings)} (
                    id, job_name, job_param, status, created_at, scheduled_start_at, can_be_restarted, sequence_id
                )
                SELECT $1, $2, $3, {(int)JobStatus.WaitingPrev}, $5, $6, $7, $8
                WHERE NOT EXISTS (SELECT 1 FROM try_ins)
            "
            : @$"
                WITH try_ins AS (
                    INSERT INTO {TableName.Jobs(settings)} (
                        id, job_name, job_param, status, created_at, scheduled_start_at, can_be_restarted, sequence_id
                    )
                    VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
                    ON CONFLICT (sequence_id) WHERE (status = {(int)JobStatus.Scheduled}) DO NOTHING
                    RETURNING 1 AS inserted
                )
                INSERT INTO {TableName.Jobs(settings)} (
                    id, job_name, job_param, status, created_at, scheduled_start_at, can_be_restarted, sequence_id
                )
                SELECT $1, $2, $3, {(int)JobStatus.WaitingPrev}, $5, $6, $7, $8
                WHERE NOT EXISTS (SELECT 1 FROM try_ins)
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
            if (job.SequenceId != null)
            {
                var cmd = new NpgsqlBatchCommand(_commandWithSequenceIdText)
                {
                    Parameters =
                    {
                        new() { Value = job.Id },                                   // $1
                        new() { Value = job.JobName },                              // $2
                        new() { Value = (object?)job.JobParam ?? DBNull.Value },    // $3
                        new() { Value = (int)JobStatus.Scheduled },                 // $4
                        new() { Value = job.CreatedAt },                            // $5
                        new() { Value = job.ScheduledStartAt },                     // $6
                        new() { Value = job.CanBeRestarted },                       // $7
                        new() { Value = job.SequenceId }                            // $8
                    }
                };
                batch.BatchCommands.Add(cmd);
            }
            else
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
}
