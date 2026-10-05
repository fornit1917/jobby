using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetRecurrentJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetRecurrentJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<PagedResult<DashboardRecurrentJobDto>> ExecuteAsync(DashboardRecurrentJobsQuery query,
        CancellationToken cancellationToken)
    {
        var where = new List<string> { "schedule IS NOT NULL" };
        var args = new List<object>();
        if (!string.IsNullOrWhiteSpace(query.JobNameSearch))
        {
            args.Add(query.JobNameSearch);
            where.Add($"job_name ILIKE '%' || ${args.Count} || '%'");
        }

        var whereSql = "WHERE " + string.Join(" AND ", where);

        var pageSize = Math.Clamp(query.PageSize, 1, 500);
        var page = Math.Max(query.Page, 1);

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

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

        var sql = $"""

                               SELECT id, job_name, schedule, scheduler_type, queue_name, status,
                                      scheduled_start_at, last_started_at, last_finished_at, job_param
                               FROM {_jobsTable}
                               {whereSql}
                               ORDER BY job_name ASC, id ASC
                               LIMIT {limitParam} OFFSET {offsetParam}
                   """;

        var items = new List<DashboardRecurrentJobDto>();
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            foreach (var a in pageArgs)
            {
                cmd.Parameters.Add(new NpgsqlParameter { Value = a });
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new DashboardRecurrentJobDto(
                    Id: reader.GetGuid(reader.GetOrdinal("id")),
                    JobName: reader.GetString(reader.GetOrdinal("job_name")),
                    Schedule: reader.GetString(reader.GetOrdinal("schedule")),
                    SchedulerType: reader.GetNullableString("scheduler_type"),
                    QueueName: reader.GetString(reader.GetOrdinal("queue_name")),
                    Status: (JobStatus)reader.GetInt32(reader.GetOrdinal("status")),
                    ScheduledStartAt: reader.GetDateTime(reader.GetOrdinal("scheduled_start_at")),
                    LastStartedAt: reader.GetNullableDatetime("last_started_at"),
                    LastFinishedAt: reader.GetNullableDatetime("last_finished_at"),
                    JobParam: reader.GetNullableString("job_param")));
            }
        }

        return new PagedResult<DashboardRecurrentJobDto>(
            Items: items, TotalCount: total, Page: page, PageSize: pageSize);
    }
}