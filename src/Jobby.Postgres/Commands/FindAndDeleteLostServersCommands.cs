using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class FindAndDeleteLostServersCommands
{
    private readonly string _deleteByIntervalText;
    private readonly string _deleteByMinHeartbeatText;

    public FindAndDeleteLostServersCommands(PostgresqlStorageSettings settings)
    {
        _deleteByIntervalText = $@"
            DELETE FROM {DbName.Servers(settings)}
            WHERE id IN (
                SELECT id FROM {DbName.Servers(settings)}
                WHERE heartbeat_ts < now() - make_interval(secs => $1)
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id
        ";
        
        // Legacy path: compares against an absolute timestamp computed on the application clock
        _deleteByMinHeartbeatText = $@"
            DELETE FROM {DbName.Servers(settings)}
            WHERE id IN (
                SELECT id FROM {DbName.Servers(settings)}
                WHERE heartbeat_ts < $1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id
        ";
    }

    public Task ExecuteInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction? tr, TimeSpan maxNoHeartbeatInterval, List<string> deletedServerIds)
    {
        return ExecuteInTransactionAsync(conn, tr, _deleteByIntervalText, maxNoHeartbeatInterval.TotalSeconds, deletedServerIds);
    }

    public Task ExecuteInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction? tr, DateTime minLastHeartbeat, List<string> deletedServerIds)
    {
        return ExecuteInTransactionAsync(conn, tr, _deleteByMinHeartbeatText, minLastHeartbeat, deletedServerIds);
    }

    private static async Task ExecuteInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction? tr, string commandText,
        object parameter, List<string> deletedServerIds)
    {
        deletedServerIds.Clear();

        await using var cmd = new NpgsqlCommand(commandText, conn, tr)
        {
            Parameters =
            {
                new() { Value = parameter }
            }
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return;
        }

        while (true)
        {
            var hasRow = await reader.ReadAsync();
            if (!hasRow)
            {
                break;
            }

            var serverId = reader.GetString(reader.GetOrdinal("id"));
            deletedServerIds.Add(serverId);
        }
    }
}
