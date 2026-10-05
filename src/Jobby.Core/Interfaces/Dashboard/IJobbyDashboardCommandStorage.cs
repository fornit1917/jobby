namespace Jobby.Core.Interfaces.Dashboard;

public interface IJobbyDashboardCommandStorage
{
    Task<int> TriggerJobsAsync(IReadOnlyList<Guid> ids, DateTime now, CancellationToken cancellationToken = default);

    Task<int> RetryJobsAsync(IReadOnlyList<Guid> ids, DateTime now, CancellationToken cancellationToken = default);

    Task<int> DeleteJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> DeleteRecurrentJobsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<int> RequestGroupUnlockAsync(string groupId, DateTime now, CancellationToken cancellationToken = default);
}
