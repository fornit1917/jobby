using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetJobByIdCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetJobByIdCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<DashboardJobDetailsDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT id, job_name, status, queue_name, created_at, scheduled_start_at,
                   last_started_at, last_finished_at, started_count, server_id,
                   job_param, error, schedule, scheduler_type, next_job_id,
                   serializable_group_id, lock_group_if_failed, is_exclusive, can_be_restarted, is_group_locker
            FROM {_jobsTable}
            WHERE id = $1";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = id });
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DashboardJobDetailsDto(
            Id: reader.GetGuid(reader.GetOrdinal("id")),
            JobName: reader.GetString(reader.GetOrdinal("job_name")),
            Status: (JobStatus)reader.GetInt32(reader.GetOrdinal("status")),
            QueueName: reader.GetString(reader.GetOrdinal("queue_name")),
            CreatedAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
            ScheduledStartAt: reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
            LastStartedAt: reader.GetNullableDatetime("last_started_at"),
            LastFinishedAt: reader.GetNullableDatetime("last_finished_at"),
            StartedCount: reader.GetInt32(reader.GetOrdinal("started_count")),
            ServerId: reader.GetNullableString("server_id"),
            JobParam: reader.GetNullableString("job_param"),
            Error: reader.GetNullableString("error"),
            Schedule: reader.GetNullableString("schedule"),
            SchedulerType: reader.GetNullableString("scheduler_type"),
            NextJobId: reader.GetNullableGuid("next_job_id"),
            SerializableGroupId: reader.GetNullableString("serializable_group_id"),
            LockGroupIfFailed: reader.GetBooleanOrFalse("lock_group_if_failed"),
            IsExclusive: reader.GetBoolean(reader.GetOrdinal("is_exclusive")),
            CanBeRestarted: reader.GetBoolean(reader.GetOrdinal("can_be_restarted")),
            IsGroupLocker: reader.GetBoolean(reader.GetOrdinal("is_group_locker")));
    }
}
