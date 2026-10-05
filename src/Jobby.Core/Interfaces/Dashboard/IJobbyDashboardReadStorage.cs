using Jobby.Core.Models.Dashboard;

namespace Jobby.Core.Interfaces.Dashboard;

public interface IJobbyDashboardReadStorage
{
    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query, CancellationToken cancellationToken = default);
    Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(DashboardRecurrentJobsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query, CancellationToken cancellationToken = default);
    Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);
}
