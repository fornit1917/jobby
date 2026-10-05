using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetQueuesCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetQueuesCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<IReadOnlyList<DashboardQueueDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var sql = $@"SELECT queue_name, status, COUNT(*) AS cnt
                     FROM {_jobsTable}
                     WHERE schedule IS NULL
                     GROUP BY queue_name, status
                     ORDER BY queue_name";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var byQueue = new Dictionary<string, List<DashboardStatusCount>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var queue = reader.GetString(reader.GetOrdinal("queue_name"));
            var status = (JobStatus)reader.GetInt32(reader.GetOrdinal("status"));
            var cnt = reader.GetInt64(reader.GetOrdinal("cnt"));
            if (!byQueue.TryGetValue(queue, out var list))
            {
                list = new List<DashboardStatusCount>();
                byQueue[queue] = list;
            }

            list.Add(new DashboardStatusCount(status, cnt));
        }

        return byQueue
            .Select(kv => new DashboardQueueDto(QueueName: kv.Key, StatusCounts: kv.Value))
            .ToList();
    }
}