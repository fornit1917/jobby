using Jobby.Core.Interfaces.Dashboard;
using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard;

internal sealed class DashboardDataReader : IDashboardDataReader
{
    private readonly IJobbyDashboardReadStorage _readStorage;
    private readonly IJobbyDashboardCommandStorage? _commandStorage;
    private readonly JobbyDashboardOptions _options;

    public DashboardDataReader(
        IJobbyDashboardReadStorage readStorage,
        IEnumerable<IJobbyDashboardCommandStorage> commandStorage,
        JobbyDashboardOptions options)
    {
        _readStorage = readStorage;
        _commandStorage = commandStorage.FirstOrDefault();
        _options = options;
    }

    public bool ManagementEnabled => _commandStorage is not null && !_options.ReadOnly;

    public Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return _readStorage.GetStatsAsync(cancellationToken);
    }

    public Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query, CancellationToken cancellationToken = default)
    {
        return _readStorage.GetJobsAsync(query, cancellationToken);
    }

    public Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _readStorage.GetJobByIdAsync(id, cancellationToken);
    }

    public Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(
        DashboardRecurrentJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _readStorage.GetRecurrentJobsAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return _readStorage.GetServersAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        return _readStorage.GetQueuesAsync(cancellationToken);
    }

    public Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(
        LockedGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _readStorage.GetLockedGroupsAsync(query, cancellationToken);
    }

    public Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return _readStorage.GetLockedGroupByIdAsync(groupId, cancellationToken);
    }

    public Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return RequireManagement().TriggerJobsAsync(ids, DateTime.UtcNow, cancellationToken);
    }

    public Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return RequireManagement().RetryJobsAsync(ids, DateTime.UtcNow, cancellationToken);
    }

    public Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return RequireManagement().DeleteJobsAsync(ids, cancellationToken);
    }

    public Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        return RequireManagement().DeleteRecurrentJobsAsync(ids, cancellationToken);
    }

    public Task<int> RequestGroupUnlockAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return RequireManagement().RequestGroupUnlockAsync(groupId, DateTime.UtcNow, cancellationToken);
    }

    private IJobbyDashboardCommandStorage RequireManagement()
    {
        if (!ManagementEnabled)
        {
            throw new InvalidOperationException(
                "Dashboard management is disabled. Register an IJobbyDashboardCommandStorage and ensure JobbyDashboardOptions.ReadOnly is false.");
        }

        return _commandStorage!;
    }
}