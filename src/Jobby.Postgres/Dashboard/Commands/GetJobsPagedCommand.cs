using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetJobsPagedCommand
{
    // The most rows the per-partition query may read (partitions * (offset + page size)).
    // Requests above it use the single-scan query, whose cost does not grow with the page number.
    internal const int MaxPartitionedRows = 20_000;

    private const string Columns =
        "id, job_name, status, queue_name, created_at, scheduled_start_at, " +
        "last_started_at, last_finished_at, started_count, server_id, is_group_locker";

    private readonly NpgsqlDataSource _dataSource;
    private readonly QueueStatusCountsCache _countsCache;
    private readonly string _jobsTable;
    private readonly string _allPartitionsSql;
    private readonly string _queuePartitionsSql;

    public GetJobsPagedCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings,
        QueueStatusCountsCache countsCache)
    {
        _dataSource = dataSource;
        _countsCache = countsCache;
        _jobsTable = DbName.Jobs(settings);

        // Both queries walk the (queue_name, status, scheduled_start_at) index with one probe per distinct
        // value instead of reading every row. Recurrent definitions are not filtered out here: a pair that
        // only they use just produces an empty partition.
        _allPartitionsSql = $@"
            WITH RECURSIVE pairs AS (
                (SELECT queue_name, status FROM {_jobsTable} ORDER BY queue_name, status LIMIT 1)
                UNION ALL
                SELECT n.queue_name, n.status
                FROM pairs p
                CROSS JOIN LATERAL (
                    SELECT j.queue_name, j.status FROM {_jobsTable} j
                    WHERE (j.queue_name, j.status) > (p.queue_name, p.status)
                    ORDER BY j.queue_name, j.status
                    LIMIT 1) n
            )
            SELECT queue_name, status FROM pairs";

        _queuePartitionsSql = $@"
            WITH RECURSIVE statuses AS (
                (SELECT status FROM {_jobsTable} WHERE queue_name = $1 ORDER BY status LIMIT 1)
                UNION ALL
                SELECT n.status
                FROM statuses s
                CROSS JOIN LATERAL (
                    SELECT j.status FROM {_jobsTable} j
                    WHERE j.queue_name = $1 AND j.status > s.status
                    ORDER BY j.status
                    LIMIT 1) n
            )
            SELECT $1 AS queue_name, status FROM statuses";
    }

    public async Task<PagedResult<DashboardJobListItemDto>> ExecuteAsync(DashboardJobsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JobStatus>? statuses = query.Statuses is { Count: > 0 }
            ? query.Statuses.Distinct().ToArray()
            : query.Status is { } single
                ? new[] { single }
                : null;
        var queueName = string.IsNullOrWhiteSpace(query.QueueName) ? null : query.QueueName;
        var search = string.IsNullOrWhiteSpace(query.JobNameSearch) ? null : query.JobNameSearch;

        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var page = Math.Max(query.Page, 1);
        var offset = ((long)page - 1) * pageSize;

        // Queue and status are the only filters the shared queue/status counts can answer.
        var filteredByQueueAndStatusOnly = search is null && query.ScheduledFrom is null && query.ScheduledTo is null;
        var (whereSql, args) = BuildWhere(statuses, queueName, search, query.ScheduledFrom, query.ScheduledTo);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        List<DashboardJobListItemDto>? items = null;
        if (filteredByQueueAndStatusOnly && query.SortBy == DashboardJobsSortBy.ScheduledStartAt)
        {
            items = await TryReadByPartitionsAsync(conn, queueName, statuses, query.SortDescending,
                pageSize, offset, cancellationToken);
        }

        items ??= await ReadBySingleScanAsync(conn, whereSql, args, query.SortBy, query.SortDescending,
            pageSize, offset, cancellationToken);

        // The total never depends on the page, so it stays correct for an out-of-range page.
        // Counts already loaded for the statistics are reused; this command never starts that aggregate itself,
        // because its own count is cheaper whenever the filter is selective.
        long total;
        if (filteredByQueueAndStatusOnly && _countsCache.TryGetLoaded(out var counts))
        {
            total = counts.Count(queueName, statuses);
            if (items.Count > 0)
            {
                // Those counts can be slightly older than the page; never report fewer jobs than it proves exist.
                total = Math.Max(total, offset + items.Count);
            }
        }
        else
        {
            total = await CountAsync(conn, whereSql, args, cancellationToken);
        }

        return new PagedResult<DashboardJobListItemDto>(
            Items: items, TotalCount: total, Page: page, PageSize: pageSize);
    }

    // Reads the first rows of every (queue, status) partition through the existing
    // (queue_name, status, scheduled_start_at) index and merges them. A row beyond offset + page size in its
    // own partition cannot be on the requested page, so the result equals the single-scan query.
    // Returns null when that would read too many rows.
    private async Task<List<DashboardJobListItemDto>?> TryReadByPartitionsAsync(NpgsqlConnection conn,
        string? queueName, IReadOnlyList<JobStatus>? statuses, bool descending, int pageSize, long offset,
        CancellationToken cancellationToken)
    {
        var rowsPerPartition = offset + pageSize;
        if (rowsPerPartition > MaxPartitionedRows)
        {
            return null;
        }

        var queueNames = new List<string>();
        var statusValues = new List<int>();
        await using (var partitionsCmd = new NpgsqlCommand(
                         queueName is null ? _allPartitionsSql : _queuePartitionsSql, conn))
        {
            if (queueName is not null)
            {
                partitionsCmd.Parameters.Add(new NpgsqlParameter { Value = queueName });
            }

            await using var reader = await partitionsCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var status = reader.GetInt32(1);
                if (statuses is null || statuses.Contains((JobStatus)status))
                {
                    queueNames.Add(reader.GetString(0));
                    statusValues.Add(status);
                }
            }
        }

        if (queueNames.Count == 0)
        {
            return [];
        }

        if (queueNames.Count * rowsPerPartition > MaxPartitionedRows)
        {
            return null;
        }

        var direction = descending ? "DESC" : "ASC";
        var sql = $@"
            SELECT j.*
            FROM unnest($1::text[], $2::integer[]) AS p(queue_name, status)
            CROSS JOIN LATERAL (
                SELECT {Columns}
                FROM {_jobsTable}
                WHERE queue_name = p.queue_name AND status = p.status AND schedule IS NULL
                ORDER BY scheduled_start_at {direction}, id {direction}
                LIMIT $3) j
            ORDER BY j.scheduled_start_at {direction}, j.id {direction}
            LIMIT $4 OFFSET $5";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = queueNames.ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = statusValues.ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter { Value = rowsPerPartition });
        cmd.Parameters.Add(new NpgsqlParameter { Value = pageSize });
        cmd.Parameters.Add(new NpgsqlParameter { Value = offset });
        return await ReadItemsAsync(cmd, cancellationToken);
    }

    private async Task<List<DashboardJobListItemDto>> ReadBySingleScanAsync(NpgsqlConnection conn, string whereSql,
        List<object> args, DashboardJobsSortBy sortBy, bool descending, int pageSize, long offset,
        CancellationToken cancellationToken)
    {
        var orderColumn = sortBy switch
        {
            DashboardJobsSortBy.JobName => "job_name",
            DashboardJobsSortBy.Status => "status",
            DashboardJobsSortBy.CreatedAt => "created_at",
            _ => "scheduled_start_at"
        };
        var orderDir = descending ? "DESC" : "ASC";

        var pageArgs = new List<object>(args) { pageSize };
        var limitParam = $"${pageArgs.Count}";
        pageArgs.Add(offset);
        var offsetParam = $"${pageArgs.Count}";

        var sql = $@"
            SELECT {Columns}
            FROM {_jobsTable}
            {whereSql}
            ORDER BY {orderColumn} {orderDir}, id {orderDir}
            LIMIT {limitParam} OFFSET {offsetParam}";

        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var a in pageArgs)
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = a });
        }

        return await ReadItemsAsync(cmd, cancellationToken);
    }

    private async Task<long> CountAsync(NpgsqlConnection conn, string whereSql, List<object> args,
        CancellationToken cancellationToken)
    {
        await using var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {_jobsTable} {whereSql}", conn);
        foreach (var a in args)
        {
            countCmd.Parameters.Add(new NpgsqlParameter { Value = a });
        }

        return (long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;
    }

    private static (string WhereSql, List<object> Args) BuildWhere(IReadOnlyList<JobStatus>? statuses,
        string? queueName, string? search, DateTime? scheduledFrom, DateTime? scheduledTo)
    {
        var where = new List<string> { "schedule IS NULL" };
        var args = new List<object>();

        if (statuses is not null)
        {
            args.Add(statuses.Select(s => (int)s).ToArray());
            where.Add($"status = ANY(${args.Count})");
        }

        if (queueName is not null)
        {
            args.Add(queueName);
            where.Add($"queue_name = ${args.Count}");
        }

        if (search is not null)
        {
            args.Add(search);
            where.Add($"job_name ILIKE '%' || ${args.Count} || '%'");
        }

        if (scheduledFrom is { } from)
        {
            args.Add(from);
            where.Add($"scheduled_start_at >= ${args.Count}");
        }

        if (scheduledTo is { } to)
        {
            args.Add(to);
            where.Add($"scheduled_start_at <= ${args.Count}");
        }

        return ("WHERE " + string.Join(" AND ", where), args);
    }

    private static async Task<List<DashboardJobListItemDto>> ReadItemsAsync(NpgsqlCommand cmd,
        CancellationToken cancellationToken)
    {
        var items = new List<DashboardJobListItemDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new DashboardJobListItemDto(
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
                IsGroupLocker: reader.GetBoolean(reader.GetOrdinal("is_group_locker"))));
        }

        return items;
    }
}
