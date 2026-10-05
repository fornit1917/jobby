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

    public PostgresqlJobbyDashboardCommandStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings)
    {
        _triggerJobsCommand = new TriggerJobsCommand(dataSource, settings);
        _retryJobsCommand = new RetryJobsCommand(dataSource, settings);
        _deleteJobsCommand = new DeleteJobsCommand(dataSource, settings);
        _deleteRecurrentJobsCommand = new DeleteRecurrentJobsCommand(dataSource, settings);
        _requestGroupUnlockCommand = new RequestGroupUnlockCommand(dataSource, settings);
    }

    public Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
        CancellationToken cancellationToken = default)
        => _triggerJobsCommand.ExecuteAsync(ids, now, cancellationToken);

    public Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, DateTime now,
        CancellationToken cancellationToken = default)
        => _retryJobsCommand.ExecuteAsync(ids, now, cancellationToken);

    public Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => _deleteJobsCommand.ExecuteAsync(ids, cancellationToken);

    public Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
        => _deleteRecurrentJobsCommand.ExecuteAsync(ids, cancellationToken);

    public Task<int> RequestGroupUnlockAsync(string groupId, DateTime now,
        CancellationToken cancellationToken = default)
        => _requestGroupUnlockCommand.ExecuteAsync(groupId, now, cancellationToken);
}