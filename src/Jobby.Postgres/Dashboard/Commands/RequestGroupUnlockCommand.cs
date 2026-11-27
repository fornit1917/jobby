using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class RequestGroupUnlockCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _sql;

    public RequestGroupUnlockCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        // Guard stale/crafted requests: the unlock finalizer deletes the current locker.
        _sql = $@"
            INSERT INTO {DbName.UnlockingGroups(settings)}(group_id, created_at)
            SELECT $1, $2
            WHERE EXISTS (
                SELECT 1 FROM {DbName.Jobs(settings)} j
                WHERE j.serializable_group_id = $1 AND ({GroupLockSql.StuckLocker("j", settings)}) AND j.schedule IS NULL
            )
            ON CONFLICT (group_id) DO NOTHING";
    }

    public async Task<int> ExecuteAsync(string groupId, DateTime now, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(_sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = groupId });
        cmd.Parameters.Add(new NpgsqlParameter { Value = now });
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
