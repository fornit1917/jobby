using Jobby.Core.Models.Dashboard;
using Jobby.Dashboard;

namespace Jobby.Tests.Dashboard;

internal sealed class FakeDashboardDataReader : IDashboardDataReader
{
    public bool ManagementEnabled { get; set; }

    public int ManagementAffected { get; set; } = 1;

    public IReadOnlyList<Guid>? LastTriggerIds { get; private set; }
    public IReadOnlyList<Guid>? LastRetryIds { get; private set; }
    public IReadOnlyList<Guid>? LastDeleteIds { get; private set; }
    public IReadOnlyList<Guid>? LastDeleteRecurrentIds { get; private set; }

    private DashboardStatsDto Stats { get; set; } = new(StatusCounts: []);

    private PagedResult<DashboardJobListItemDto> Jobs { get; set; }
        = new(Items: [], TotalCount: 0, Page: 1, PageSize: 50);

    public DashboardJobDetailsDto? JobById { get; set; }
    private IReadOnlyList<DashboardServerDto> Servers { get; set; } = [];
    private IReadOnlyList<DashboardQueueDto> Queues { get; set; } = [];

    private PagedResult<DashboardRecurrentJobDto> Recurrent { get; set; }
        = new(Items: [], TotalCount: 0, Page: 1, PageSize: 50);

    public DashboardJobsQuery? LastJobsQuery { get; private set; }
    public DashboardRecurrentJobsQuery? LastRecurrentQuery { get; private set; }
    private int GetRecurrentCallCount { get; set; }
    private int GetJobsCallCount { get; set; }

    public PagedResult<LockedGroupDto> LockedGroups { get; set; }
        = new(Items: [], TotalCount: 0, Page: 1, PageSize: 50);

    public LockedGroupDetailsDto? LockedGroupDetails { get; set; }
    public string? LastUnlockGroupId { get; private set; }
    public bool UnlockInserted { get; set; } = true;
    public LockedGroupsQuery? LastLockedGroupsQuery { get; private set; }

    public Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Stats);
    }

    public Task<PagedResult<DashboardJobListItemDto>> GetJobsAsync(DashboardJobsQuery query, CancellationToken cancellationToken = default)
    {
        GetJobsCallCount++;
        LastJobsQuery = query;
        return Task.FromResult(Jobs);
    }

    public Task<DashboardJobDetailsDto?> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(JobById);
    }

    public Task<PagedResult<DashboardRecurrentJobDto>> GetRecurrentJobsAsync(
        DashboardRecurrentJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        GetRecurrentCallCount++;
        LastRecurrentQuery = query;
        return Task.FromResult(Recurrent);
    }

    public Task<IReadOnlyList<DashboardServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Servers);
    }

    public Task<IReadOnlyList<DashboardQueueDto>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Queues);
    }

    public Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        LastTriggerIds = ids;
        return Task.FromResult(ManagementAffected);
    }

    public Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        LastRetryIds = ids;
        return Task.FromResult(ManagementAffected);
    }

    public Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        LastDeleteIds = ids;
        return Task.FromResult(ManagementAffected);
    }

    public Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        LastDeleteRecurrentIds = ids;
        return Task.FromResult(ManagementAffected);
    }

    public Task<PagedResult<LockedGroupDto>> GetLockedGroupsAsync(LockedGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        LastLockedGroupsQuery = query;
        return Task.FromResult(LockedGroups);
    }

    public Task<LockedGroupDetailsDto?> GetLockedGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LockedGroupDetails);
    }

    public Task<int> RequestGroupUnlockAsync(string groupId, CancellationToken cancellationToken = default)
    {
        LastUnlockGroupId = groupId;
        return Task.FromResult(UnlockInserted ? 1 : 0);
    }
}