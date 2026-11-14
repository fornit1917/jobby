using Jobby.Core.Models;
using Jobby.Postgres.Helpers;
using Npgsql;

namespace Jobby.Postgres.Dashboard.Commands;

internal sealed class RetryJobsCommand
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _sql;

    public RetryJobsCommand(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _dataSource = dataSource;

        _sql = $@"
            UPDATE {DbName.Jobs(settings)}
            SET status = {(int)JobStatus.Scheduled},
                scheduled_start_at = $2,
                started_count = 0,
                error = NULL,
                server_id = NULL,
                last_started_at = NULL,
                last_finished_at = NULL
            WHERE id = ANY($1)
              AND (status = {(int)JobStatus.Failed} OR status = {(int)JobStatus.Frozen})
              AND schedule IS NULL
              -- Never retry the job that is actively locking a serializable group: requeuing it would
              -- unlock the group while its frozen siblings stay Frozen forever. Frozen siblings
              -- themselves are not lockers, so they remain retryable (and are simply re-frozen).
              AND is_group_locker = FALSE";
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
