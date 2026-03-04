using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;

public interface IPermanentLocksStorage
{
    Task FreezePermanentLockedJobsFromTopOfQueue(string queueName, int batchSize, List<JobWithGroupModel> frozenIds);
    Task<GroupUnlockingStatusModel?> UnfreezeBatchAndUnlockIfAllUnfrozen();
}