using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class DeleteRecurrentJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _sql;

    public DeleteRecurrentJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        // Never delete a recurrent row while it is the serializable-group locker.
        _sql = $@"DELETE FROM {DbName.Jobs(settings)} WHERE id = ANY($1) AND schedule IS NOT NULL AND is_group_locker = FALSE";
    }

    public async Task<int> ExecuteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(_sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids });
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
