using Jobby.Core.Models;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetStatsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _jobsTable;

    public GetStatsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _jobsTable = DbName.Jobs(settings);
    }

    public async Task<DashboardStatsDto> ExecuteAsync(CancellationToken cancellationToken)
    {
        var sql = $@"SELECT status, COUNT(*) AS cnt
                     FROM {_jobsTable}
                     WHERE schedule IS NULL
                     GROUP BY status";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var counts = new List<DashboardStatusCount>();
        while (await reader.ReadAsync(cancellationToken))
        {
            counts.Add(new DashboardStatusCount(
                (JobStatus)reader.GetInt32(reader.GetOrdinal("status")),
                reader.GetInt64(reader.GetOrdinal("cnt"))));
        }

        return new DashboardStatsDto(StatusCounts: counts);
    }
}