using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class SendHeartbeatCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _commandText;

    public SendHeartbeatCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _commandText = $@"
            INSERT INTO {TableName.Servers(settings)}
                (id, heartbeat_ts)
                VALUES ($1, $2)
            ON CONFLICT (id) DO
            UPDATE
                SET heartbeat_ts = $2
        ";
    }

    public async Task ExecuteAsync(string serverId, DateTime ts)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = serverId },
                new() { Value = ts },
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
