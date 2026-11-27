using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class GetServersCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _serversTable;

    public GetServersCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _serversTable = DbName.Servers(settings);
    }

    public async Task<IReadOnlyList<DashboardServerDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var sql = $"SELECT id, heartbeat_ts FROM {_serversTable} ORDER BY id";
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var result = new List<DashboardServerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new DashboardServerDto(
                Id: reader.GetString(reader.GetOrdinal("id")),
                HeartbeatTs: reader.GetDateTime(reader.GetOrdinal("heartbeat_ts"))));
        }

        return result;
    }
}