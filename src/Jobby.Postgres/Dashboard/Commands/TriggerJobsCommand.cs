using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class TriggerJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _sql;

    public TriggerJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;
        _sql = $@"
            UPDATE {DbName.Jobs(settings)}
            SET scheduled_start_at = $2
            WHERE id = ANY($1) AND status = {(int)JobStatus.Scheduled} AND schedule IS NULL";
    }

    public async Task<int> ExecuteAsync(IReadOnlyList<Guid> ids, DateTime now, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(_sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = ids });
        cmd.Parameters.Add(new NpgsqlParameter { Value = now });
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
