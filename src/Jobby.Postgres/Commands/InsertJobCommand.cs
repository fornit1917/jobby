using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class InsertJobCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public InsertJobCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        var jobsTableName = DbName.Jobs(settings);

        _commandText = @$"
            INSERT INTO {jobsTableName} (
                id,
                job_name,
                job_param,
                status,
                created_at,
                scheduled_start_at,
                schedule,
                next_job_id,
                can_be_restarted,
                queue_name,
                serializable_group_id,
                lock_group_if_failed,
                is_exclusive,
                scheduler_type
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
            ON CONFLICT (job_name) WHERE is_exclusive=true DO
            UPDATE SET
                job_param = $3,
	            schedule = $7,
	            scheduled_start_at = case
                    when {jobsTableName}.schedule <> $7 or {jobsTableName}.scheduler_type <> $14
                        then $6
                        else {jobsTableName}.scheduled_start_at
                    end,
                can_be_restarted = $9,
                queue_name = $10,
                serializable_group_id = $11,
                scheduler_type = $14
            RETURNING id
        ";
    }

    private NpgsqlCommand CreateCommand(NpgsqlConnection conn, JobCreationModel job)
    {
        return new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = job.Id },                                           // 1
                new() { Value = job.JobName },                                      // 2
                new() { Value = (object?)job.JobParam ?? DBNull.Value },            // 3
                new() { Value = (int)job.Status },                                  // 4
                new() { Value = job.CreatedAt },                                    // 5
                new() { Value = job.ScheduledStartAt },                             // 6
                new() { Value = (object?)job.Schedule ?? DBNull.Value },                // 7
                new() { Value = (object?)job.NextJobId ?? DBNull.Value },           // 8
                new() { Value = job.CanBeRestarted },                               // 9
                new() { Value = job.QueueName },                                    // 10
                new() { Value = (object?)job.SerializableGroupId ?? DBNull.Value }, // 11
                new() { Value = job.LockGroupIfFailed },                            // 12
                new() { Value = job.IsExclusive },                                  // 13
                new() { Value = (object?)job.SchedulerType ?? DBNull.Value },       // 14
            }
        };
    }

    public async Task<Guid> ExecuteAsync(JobCreationModel job)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = CreateCommand(conn, job);
        var id = await cmd.ExecuteScalarAsync();
        return id is Guid guid ? guid : Guid.Empty;
    }

    public Guid Execute(JobCreationModel job)
    {
        using var conn = _dataSource.OpenConnection();
        using var cmd = CreateCommand(conn, job);
        var id = cmd.ExecuteScalar();
        return id is Guid guid ? guid : Guid.Empty;
    }
}
