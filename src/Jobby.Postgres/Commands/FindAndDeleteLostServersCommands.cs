using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Commands;

internal class FindAndDeleteLostServersCommands
{
    private readonly string _commandText;

    public FindAndDeleteLostServersCommands(PostgresqlStorageSettings settings)
    {
        _commandText = $@"
            DELETE FROM {TableName.Servers(settings)}
            WHERE id IN (
                SELECT id FROM {TableName.Servers(settings)}
                WHERE heartbeat_ts < $1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id
        ";
    }

    public async Task ExecuteInTransactionAsync(NpgsqlConnection conn, NpgsqlTransaction? tr, DateTime minLastHearbeat, List<string> deletedServerIds)
    {
        deletedServerIds.Clear();

        await using var cmd = new NpgsqlCommand(_commandText, conn, tr)
        {
            Parameters =
            {
                new() { Value = minLastHearbeat }
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
