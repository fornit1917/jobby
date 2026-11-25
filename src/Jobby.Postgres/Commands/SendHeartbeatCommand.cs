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
            INSERT INTO {DbName.Servers(settings)}
                (id, heartbeat_ts)
                VALUES ($1, now())
            ON CONFLICT (id) DO
            UPDATE
                SET heartbeat_ts = now()
        ";
    }

    public async Task ExecuteAsync(string serverId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(_commandText, conn)
        {
            Parameters =
            {
                new() { Value = serverId },
            }
        };
        await cmd.ExecuteNonQueryAsync();
    }
}
