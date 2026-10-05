using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetJobsPagedCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetJobsPagedCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<PagedResult<DashboardJobListItemDto>> ExecuteAsync(DashboardJobsQuery query,
        CancellationToken cancellationToken)
    {
        var where = new List<string> { "schedule IS NULL" };
        var args = new List<object>();

        IReadOnlyList<JobStatus>? statuses = query.Statuses is { Count: > 0 }
            ? query.Statuses
            : query.Status is { } single
                ? new[] { single }
                : null;
        if (statuses is not null)
        {
            args.Add(statuses.Select(s => (int)s).ToArray());
            where.Add($"status = ANY(${args.Count})");
        }

        if (!string.IsNullOrWhiteSpace(query.QueueName))
        {
            args.Add(query.QueueName);
            where.Add($"queue_name = ${args.Count}");
        }

        if (!string.IsNullOrWhiteSpace(query.JobNameSearch))
        {
            args.Add(query.JobNameSearch);
            where.Add($"job_name ILIKE '%' || ${args.Count} || '%'");
        }

        if (query.CreatedFrom is { } from)
        {
            args.Add(from);
            where.Add($"created_at >= ${args.Count}");
        }

        if (query.CreatedTo is { } to)
        {
            args.Add(to);
            where.Add($"created_at <= ${args.Count}");
        }

        var whereSql = "WHERE " + string.Join(" AND ", where);
        var orderColumn = query.SortBy switch
        {
            DashboardJobsSortBy.JobName => "job_name",
            DashboardJobsSortBy.Status => "status",
            DashboardJobsSortBy.ScheduledStartAt => "scheduled_start_at",
            _ => "created_at"
        };
        var orderDir = query.SortDescending ? "DESC" : "ASC";

        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var page = Math.Max(query.Page, 1);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Total via a dedicated count query so it is correct even for an out-of-range page.
        // (COUNT(*) OVER() returns NO row when LIMIT/OFFSET yields zero rows, which would
        //  wrongly leave the total at 0 after stale pagination/filter changes.)
        long total;
        var countSql = $"SELECT COUNT(*) FROM {_jobsTable} {whereSql}";
        await using (var countCmd = new NpgsqlCommand(countSql, conn))
        {
            foreach (var a in args)
            {
                countCmd.Parameters.Add(new NpgsqlParameter { Value = a });
            }

            total = (long)(await countCmd.ExecuteScalarAsync(cancellationToken))!;
        }

        var pageArgs = new List<object>(args) { pageSize };
        var limitParam = $"${pageArgs.Count}";
        pageArgs.Add(((long)page - 1) * pageSize);
        var offsetParam = $"${pageArgs.Count}";

        var sql = $@"
            SELECT id, job_name, status, queue_name, created_at, scheduled_start_at,
                   last_started_at, last_finished_at, started_count, server_id, is_group_locker
            FROM {_jobsTable}
            {whereSql}
            ORDER BY {orderColumn} {orderDir}, id {orderDir}
            LIMIT {limitParam} OFFSET {offsetParam}";

        var items = new List<DashboardJobListItemDto>();
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            foreach (var a in pageArgs)
            {
                cmd.Parameters.Add(new NpgsqlParameter { Value = a });
            }

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
        }

        return new PagedResult<DashboardJobListItemDto>(
            Items: items, TotalCount: total, Page: page, PageSize: pageSize);
    }
}
