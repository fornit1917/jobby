using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetLockedGroupsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;
    private readonly string _unlockingGroupsTable;
    private readonly string _stuckLocker;

    public GetLockedGroupsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
        _unlockingGroupsTable = DbName.UnlockingGroups(settings);
        _stuckLocker = GroupLockSql.StuckLocker("j", settings);
    }

    public async Task<PagedResult<LockedGroupDto>> ExecuteAsync(LockedGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var page = Math.Max(query.Page, 1);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        long total;
        var countSql = $"SELECT COUNT(*) FROM {_jobsTable} j WHERE {_stuckLocker} AND j.schedule IS NULL";
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            total = (long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;
        }

        // Page the lockers FIRST (cheap: scans the partial index, joins one row per group), THEN aggregate
        // counts only for the page's groups. schedule IS NULL scopes the feature to non-recurrent jobs.
        var sql = $@"
            WITH lockers AS (
                SELECT j.serializable_group_id AS group_id, j.id AS locker_id, j.job_name AS locker_name,
                       j.status AS locker_status, j.server_id AS locker_server_id,
                       j.last_started_at AS locker_last_started_at, j.last_finished_at AS locker_last_finished_at,
                       j.created_at AS locker_created_at, j.error AS locker_error,
                       COALESCE(j.last_finished_at, j.last_started_at, j.created_at) AS stuck_since
                FROM {_jobsTable} j
                WHERE {_stuckLocker} AND j.schedule IS NULL
            ),
            lockers_page AS (
                SELECT l.*, (u.group_id IS NOT NULL) AS unlock_requested, u.created_at AS unlock_requested_at
                FROM lockers l
                LEFT JOIN {_unlockingGroupsTable} u ON u.group_id = l.group_id
                ORDER BY unlock_requested ASC, l.stuck_since ASC, l.group_id ASC
                LIMIT $1 OFFSET $2
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
                WHERE g.serializable_group_id IN (SELECT group_id FROM lockers_page)
                  AND g.schedule IS NULL
                GROUP BY g.serializable_group_id
            )
            SELECT lp.group_id, lp.locker_id, lp.locker_name, lp.locker_status, lp.locker_server_id,
                   lp.locker_last_started_at, lp.locker_last_finished_at, lp.locker_created_at, lp.locker_error,
                   lp.stuck_since, c.queues, c.frozen_count, c.scheduled_count, c.waiting_count,
                   c.non_locker_failed_count, c.oldest_frozen_at,
                   lp.unlock_requested, lp.unlock_requested_at
            FROM lockers_page lp
            JOIN counts c ON c.group_id = lp.group_id
            ORDER BY lp.unlock_requested ASC, lp.stuck_since ASC, lp.group_id ASC";

        var items = new List<LockedGroupDto>();
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
            cmd.Parameters.Add(new NpgsqlParameter { Value = ((long)page - 1) * pageSize });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapLockedGroupHeader(reader));
            }
        }

        return new PagedResult<LockedGroupDto>(Items: items, TotalCount: total, Page: page, PageSize: pageSize);
    }

    internal static LockedGroupDto MapLockedGroupHeader(NpgsqlDataReader reader) => new(
        GroupId: reader.GetString(reader.GetOrdinal("group_id")),
        Queues: reader.GetFieldValue<string[]>(reader.GetOrdinal("queues")),
        LockerId: reader.GetGuid(reader.GetOrdinal("locker_id")),
        LockerName: reader.GetString(reader.GetOrdinal("locker_name")),
        LockerStatus: (JobStatus)reader.GetInt32(reader.GetOrdinal("locker_status")),
        LockerServerId: reader.GetNullableString("locker_server_id"),
        LockerLastStartedAt: reader.GetNullableDatetime("locker_last_started_at"),
        LockerLastFinishedAt: reader.GetNullableDatetime("locker_last_finished_at"),
        LockerCreatedAt: reader.GetDateTime(reader.GetOrdinal("locker_created_at")),
        LockerError: reader.GetNullableString("locker_error"),
        StuckSince: reader.GetDateTime(reader.GetOrdinal("stuck_since")),
        FrozenCount: (int)reader.GetInt64(reader.GetOrdinal("frozen_count")),
        ScheduledCount: (int)reader.GetInt64(reader.GetOrdinal("scheduled_count")),
        WaitingCount: (int)reader.GetInt64(reader.GetOrdinal("waiting_count")),
        NonLockerFailedCount: (int)reader.GetInt64(reader.GetOrdinal("non_locker_failed_count")),
        OldestFrozenAt: reader.GetNullableDatetime("oldest_frozen_at"),
        UnlockRequested: reader.GetBoolean(reader.GetOrdinal("unlock_requested")),
        UnlockRequestedAt: reader.GetNullableDatetime("unlock_requested_at"));
}
