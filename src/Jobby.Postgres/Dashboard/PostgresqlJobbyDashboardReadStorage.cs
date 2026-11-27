using Jobby.Core.Interfaces.Dashboard;
using Jobby.Core.Models.Dashboard;
using Jobby.Postgres.Dashboard.Commands;
using Npgsql;

namespace Jobby.Postgres.Dashboard;

internal sealed class PostgresqlJobbyDashboardReadStorage : IJobbyDashboardReadStorage
{
    private readonly QueueStatusCountsCache _countsCache;
    private readonly GetServersCommand _getServersCommand;
    private readonly GetJobByIdCommand _getJobByIdCommand;
    private readonly GetJobsPagedCommand _getJobsPagedCommand;
    private readonly GetRecurrentJobsCommand _getRecurrentJobsCommand;
    private readonly GetLockedGroupsCommand _getLockedGroupsCommand;
    private readonly GetLockedGroupByIdCommand _getLockedGroupByIdCommand;

    public PostgresqlJobbyDashboardReadStorage(NpgsqlDataSource dataSource, PostgresqlStorageSettings settings,
        QueueStatusCountsCache countsCache)
    {
        _countsCache = countsCache;
        _getServersCommand = new GetServersCommand(dataSource, settings);
        _getJobByIdCommand = new GetJobByIdCommand(dataSource, settings);
        _getJobsPagedCommand = new GetJobsPagedCommand(dataSource, settings, countsCache);
        _getRecurrentJobsCommand = new GetRecurrentJobsCommand(dataSource, settings);
        _getLockedGroupsCommand = new GetLockedGroupsCommand(dataSource, settings);
        _getLockedGroupByIdCommand = new GetLockedGroupByIdCommand(dataSource, settings);
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        => (await _countsCache.GetAsync(cancellationToken)).Stats;

    public Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query,
        CancellationToken cancellationToken = default)
        => _getJobsPagedCommand.ExecuteAsync(query, cancellationToken);

    public Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _getJobByIdCommand.ExecuteAsync(id, cancellationToken);

    public Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(DashboardRecurrentJobsQuery query,
        CancellationToken cancellationToken = default)
        => _getRecurrentJobsCommand.ExecuteAsync(query, cancellationToken);

    public Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
        => _getServersCommand.ExecuteAsync(cancellationToken);

    public async Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
        => (await _countsCache.GetAsync(cancellationToken)).Queues;

    public Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query,
        CancellationToken cancellationToken = default)
        => _getLockedGroupsCommand.ExecuteAsync(query, cancellationToken);

    public Task<long> GetLockedGroupsCountAsync(CancellationToken cancellationToken = default)
        => _getLockedGroupsCommand.CountAsync(cancellationToken);

    public Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId,
        CancellationToken cancellationToken = default)
        => _getLockedGroupByIdCommand.ExecuteAsync(groupId, cancellationToken);
}
