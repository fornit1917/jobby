using Jobby.Core.Interfaces.Dashboard;
using Jobby.Postgres.Dashboard.Commands;
using Npgsql;

namespace Jobby.Postgres.Dashboard;

internal sealed class PostgresqlJobbyDashboardCommandStorage : IJobbyDashboardCommandStorage
{
    private readonly TriggerJobsCommand _triggerJobsCommand;
    private readonly RetryJobsCommand _retryJobsCommand;
    private readonly DeleteJobsCommand _deleteJobsCommand;
    private readonly DeleteRecurrentJobsCommand _deleteRecurrentJobsCommand;
    private readonly RequestGroupUnlockCommand _requestGroupUnlockCommand;
    private readonly QueueStatusCountsCache _countsCache;

    public PostgresqlJobbyDashboardCommandStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings,
        QueueStatusCountsCache countsCache)
    {
        _countsCache = countsCache;
        _triggerJobsCommand = new TriggerJobsCommand(dataSource, settings);
        _retryJobsCommand = new RetryJobsCommand(dataSource, settings);
        _deleteJobsCommand = new DeleteJobsCommand(dataSource, settings);
        _deleteRecurrentJobsCommand = new DeleteRecurrentJobsCommand(dataSource, settings);
        _requestGroupUnlockCommand = new RequestGroupUnlockCommand(dataSource, settings);
    }

    public Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
        CancellationToken cancellationToken = default)
        => _triggerJobsCommand.ExecuteAsync(ids, now, cancellationToken);

    public async Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
        CancellationToken cancellationToken = default)
    {
        var affected = await _retryJobsCommand.ExecuteAsync(ids, now, cancellationToken);
        InvalidateCountsIfChanged(affected);
        return affected;
    }

    public async Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var affected = await _deleteJobsCommand.ExecuteAsync(ids, cancellationToken);
        InvalidateCountsIfChanged(affected);
        return affected;
    }

    public Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => _deleteRecurrentJobsCommand.ExecuteAsync(ids, cancellationToken);

    public Task<int> RequestGroupUnlockAsync(string groupId, DateTime now,
        CancellationToken cancellationToken = default)
        => _requestGroupUnlockCommand.ExecuteAsync(groupId, now, cancellationToken);

    // Retry and delete move non-recurrent jobs between statuses or remove them, so the dashboard that issued
    // the action must not keep showing the previous counts. Trigger, recurrent delete and unlock requests do
    // not change those counts.
    private void InvalidateCountsIfChanged(int affected)
    {
        if (affected > 0)
        {
            _countsCache.Invalidate();
        }
    }
}