using Jobby.Core.Models.Dashboard;

namespace Jobby.Dashboard;

public interface IDashboardDataReader
{
    // False means action methods throw; callers should hide or disable management paths first.
    bool ManagementEnabled { get; }

    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query, CancellationToken cancellationToken = default);
    Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(DashboardRecurrentJobsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query, CancellationToken cancellationToken = default);
    Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);

    // Management actions: each returns the number of affected jobs (0 means the job is gone or no
    // longer in a state the action allows). Throw InvalidOperationException when ManagementEnabled is false.

    Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> RequestGroupUnlockAsync(string groupId, CancellationToken cancellationToken = default);
}
