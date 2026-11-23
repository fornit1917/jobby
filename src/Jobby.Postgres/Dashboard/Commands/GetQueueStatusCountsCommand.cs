using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetQueueStatusCountsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetQueueStatusCountsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<QueueStatusCounts> ExecuteAsync(CancellationToken cancellationToken)
    {
        // Recurrent jobs are counted by the same scan instead of a second query over the whole table.
        var sql = $@"SELECT queue_name, status, schedule IS NOT NULL AS is_recurrent, COUNT(*) AS cnt
                     FROM {_jobsTable}
                     GROUP BY queue_name, status, schedule IS NOT NULL
                     ORDER BY queue_name";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var byQueue = new Dictionary<string, List<DashboardStatusCount>>();
        var recurrentByStatus = new Dictionary<JobStatus, long>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var queue = reader.GetString(reader.GetOrdinal("queue_name"));
            var status = (JobStatus)reader.GetInt32(reader.GetOrdinal("status"));
            var cnt = reader.GetInt64(reader.GetOrdinal("cnt"));
            if (reader.GetBoolean(reader.GetOrdinal("is_recurrent")))
            {
                // Queue cards and job lists show non-recurrent jobs only, so recurrent ones are not kept per queue.
                recurrentByStatus[status] = recurrentByStatus.GetValueOrDefault(status) + cnt;
                continue;
            }

            if (!byQueue.TryGetValue(queue, out var list))
            {
                list = new List<DashboardStatusCount>();
                byQueue[queue] = list;
            }

            list.Add(new DashboardStatusCount(status, cnt));
        }

        return new QueueStatusCounts(
            byQueue
                .Select(kv => new DashboardQueueDto(QueueName: kv.Key, StatusCounts: kv.Value))
                .ToList(),
            recurrentByStatus
                .OrderBy(kv => kv.Key)
                .Select(kv => new DashboardStatusCount(kv.Key, kv.Value))
                .ToList());
    }
}
