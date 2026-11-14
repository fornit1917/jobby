using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class DeleteJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _sql;

    public DeleteJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        // Never delete running jobs, recurrent definitions, or serializable-group lockers here.
        _sql = $@"
            DELETE FROM {DbName.Jobs(settings)}
            WHERE id = ANY($1)
              AND status <> {(int)JobStatus.Processing}
              AND schedule IS NULL
              AND is_group_locker = FALSE";
    }

    public async Task<int> ExecuteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(_sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids });
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
