using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetLockedGroupByIdCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;
    private readonly string _unlockingGroupsTable;
    private readonly string _stuckLocker;

    public GetLockedGroupByIdCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
        _unlockingGroupsTable = DbName.UnlockingGroups(settings);
        _stuckLocker = GroupLockSql.StuckLocker("j", settings);
    }

    public async Task<LockedGroupDetailsDto?> ExecuteAsync(string groupId, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var headerSql = $@"
            WITH lockers AS (
                SELECT j.serializable_group_id AS group_id, j.id AS locker_id, j.job_name AS locker_name,
                       j.status AS locker_status, j.server_id AS locker_server_id,
                       j.last_started_at AS locker_last_started_at, j.last_finished_at AS locker_last_finished_at,
                       j.created_at AS locker_created_at, j.error AS locker_error,
                       COALESCE(j.last_finished_at, j.last_started_at, j.created_at) AS stuck_since
                FROM {_jobsTable} j
                WHERE {_stuckLocker} AND j.serializable_group_id = $1 AND j.schedule IS NULL
            ),
            counts AS (
                SELECT g.serializable_group_id AS group_id,
                       COUNT(*) FILTER (WHERE g.status = {(int)JobStatus.Frozen}) AS frozen_count,
                       COUNT(*) FILTER (WHERE g.status = {(int)JobStatus.Scheduled}) AS scheduled_count,
                       COUNT(*) FILTER (WHERE g.status = {(int)JobStatus.WaitingPrev}) AS waiting_count,
                       COUNT(*) FILTER (WHERE g.status = {(int)JobStatus.Failed} AND g.is_group_locker = FALSE) AS non_locker_failed_count,
                       MIN(g.scheduled_start_at) FILTER (WHERE g.status = {(int)JobStatus.Frozen}) AS oldest_frozen_at,
                       array_agg(DISTINCT g.queue_name ORDER BY g.queue_name) AS queues
                FROM {_jobsTable} g
                WHERE g.serializable_group_id = $1 AND g.schedule IS NULL
                GROUP BY g.serializable_group_id
            )
            SELECT l.group_id, l.locker_id, l.locker_name, l.locker_status, l.locker_server_id,
                   l.locker_last_started_at, l.locker_last_finished_at, l.locker_created_at, l.locker_error,
                   l.stuck_since, c.queues, c.frozen_count, c.scheduled_count, c.waiting_count,
                   c.non_locker_failed_count, c.oldest_frozen_at,
                   (u.group_id IS NOT NULL) AS unlock_requested, u.created_at AS unlock_requested_at
            FROM lockers l
            JOIN counts c ON c.group_id = l.group_id
            LEFT JOIN {_unlockingGroupsTable} u ON u.group_id = l.group_id";

        LockedGroupDto header;
        await using (var headerCmd = new NpgsqlCommand(headerSql, conn))
        {
            headerCmd.Parameters.Add(new NpgsqlParameter { Value = groupId });
            await using var reader = await headerCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            header = GetLockedGroupsCommand.MapLockedGroupHeader(reader);
        }

        var jobsSql = $@"
            SELECT id, job_name, status, scheduled_start_at, started_count, server_id, is_group_locker
            FROM {_jobsTable}
            WHERE serializable_group_id = $1 AND schedule IS NULL AND status <> {(int)JobStatus.Completed}
            ORDER BY scheduled_start_at";

        var jobs = new List<LockedGroupJobDto>();
        await using (var jobsCmd = new NpgsqlCommand(jobsSql, conn))
        {
            jobsCmd.Parameters.Add(new NpgsqlParameter { Value = groupId });
            await using var reader = await jobsCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var status = (JobStatus)reader.GetInt32(reader.GetOrdinal("status"));
                var isLocker = reader.GetBoolean(reader.GetOrdinal("is_group_locker"));
                jobs.Add(new LockedGroupJobDto(
                    Id: reader.GetGuid(reader.GetOrdinal("id")),
                    JobName: reader.GetString(reader.GetOrdinal("job_name")),
                    Status: status,
                    ScheduledStartAt: reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
                    StartedCount: reader.GetInt32(reader.GetOrdinal("started_count")),
                    ServerId: reader.GetNullableString("server_id"),
                    Role: RoleOf(isLocker, status)));
            }
        }

        return new LockedGroupDetailsDto(
            GroupId: header.GroupId,
            Queues: header.Queues,
            LockerId: header.LockerId,
            LockerName: header.LockerName,
            LockerStatus: header.LockerStatus,
            LockerServerId: header.LockerServerId,
            LockerLastStartedAt: header.LockerLastStartedAt,
            LockerLastFinishedAt: header.LockerLastFinishedAt,
            LockerCreatedAt: header.LockerCreatedAt,
            LockerError: header.LockerError,
            StuckSince: header.StuckSince,
            FrozenCount: header.FrozenCount,
            ScheduledCount: header.ScheduledCount,
            WaitingCount: header.WaitingCount,
            NonLockerFailedCount: header.NonLockerFailedCount,
            OldestFrozenAt: header.OldestFrozenAt,
            UnlockRequested: header.UnlockRequested,
            UnlockRequestedAt: header.UnlockRequestedAt,
            Jobs: jobs);
    }

    private static LockedGroupJobRole RoleOf(bool isLocker, JobStatus status) =>
        isLocker
            ? LockedGroupJobRole.Locker
            : status switch
            {
                JobStatus.Frozen => LockedGroupJobRole.Frozen,
                JobStatus.WaitingPrev => LockedGroupJobRole.WaitingPrev,
                JobStatus.Failed => LockedGroupJobRole.Failed,
                _ => LockedGroupJobRole.Scheduled,
            };
}
