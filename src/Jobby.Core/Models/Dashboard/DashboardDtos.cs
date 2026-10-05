namespace Jobby.Core.Models.Dashboard;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    long TotalCount,
    int Page,
    int PageSize);

public sealed record DashboardStatusCount(JobStatus Status, long Count);

public sealed record DashboardStatsDto(IReadOnlyList<DashboardStatusCount> StatusCounts)
{
    public long Total => StatusCounts.Sum(c => c.Count);
}

public enum DashboardJobsSortBy
{
    CreatedAt,
    ScheduledStartAt,
    JobName,
    Status
}

public sealed record DashboardJobsQuery
{
    public JobStatus? Status { get; init; }

    public IReadOnlyList<JobStatus>? Statuses { get; init; }

    public string? QueueName { get; init; }
    public string? JobNameSearch { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DashboardJobsSortBy SortBy { get; init; } = DashboardJobsSortBy.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

public sealed record DashboardJobListItemDto(
    Guid Id,
    string JobName,
    JobStatus Status,
    string QueueName,
    DateTime CreatedAt,
    DateTime ScheduledStartAt,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    int StartedCount,
    string? ServerId,
    bool IsGroupLocker);

public sealed record DashboardJobDetailsDto(
    Guid Id,
    string JobName,
    JobStatus Status,
    string QueueName,
    DateTime CreatedAt,
    DateTime ScheduledStartAt,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    int StartedCount,
    string? ServerId,
    string? JobParam,
    string? Error,
    string? Schedule,
    string? SchedulerType,
    Guid? NextJobId,
    string? SerializableGroupId,
    bool LockGroupIfFailed,
    bool IsExclusive,
    bool CanBeRestarted,
    bool IsGroupLocker);

public sealed record DashboardRecurrentJobsQuery
{
    public string? JobNameSearch { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record DashboardRecurrentJobDto(
    Guid Id,
    string JobName,
    string Schedule,
    string? SchedulerType,
    string QueueName,
    JobStatus Status,
    DateTime ScheduledStartAt,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    string? JobParam);

public sealed record DashboardServerDto(string Id, DateTime HeartbeatTs);

public sealed record DashboardQueueDto(string QueueName, IReadOnlyList<DashboardStatusCount> StatusCounts);

public sealed record LockedGroupsQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public enum LockedGroupJobRole
{
    Locker,
    Frozen,
    WaitingPrev,
    Scheduled,
    Failed
}

public sealed record LockedGroupDto(
    string GroupId,
    IReadOnlyList<string> Queues,
    Guid LockerId,
    string LockerName,
    JobStatus LockerStatus,
    string? LockerServerId,
    DateTime? LockerLastStartedAt,
    DateTime? LockerLastFinishedAt,
    DateTime LockerCreatedAt,
    string? LockerError,
    DateTime StuckSince,
    int FrozenCount,
    int ScheduledCount,
    int WaitingCount,
    int NonLockerFailedCount,
    DateTime? OldestFrozenAt,
    bool UnlockRequested,
    DateTime? UnlockRequestedAt);

public sealed record LockedGroupJobDto(
    Guid Id,
    string JobName,
    JobStatus Status,
    DateTime ScheduledStartAt,
    int StartedCount,
    string? ServerId,
    LockedGroupJobRole Role);

public sealed record LockedGroupDetailsDto(
    string GroupId,
    IReadOnlyList<string> Queues,
    Guid LockerId,
    string LockerName,
    JobStatus LockerStatus,
    string? LockerServerId,
    DateTime? LockerLastStartedAt,
    DateTime? LockerLastFinishedAt,
    DateTime LockerCreatedAt,
    string? LockerError,
    DateTime StuckSince,
    int FrozenCount,
    int ScheduledCount,
    int WaitingCount,
    int NonLockerFailedCount,
    DateTime? OldestFrozenAt,
    bool UnlockRequested,
    DateTime? UnlockRequestedAt,
    IReadOnlyList<LockedGroupJobDto> Jobs);

public sealed record UnlockGroupRequest(string GroupId);

public sealed record RequestGroupUnlockResultDto(bool Inserted);
