using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Jobby.Core.Models;

namespace Jobby.Core.Services.ServerModules.PermanentLocksCheck;

public class FreezePermanentLockedJobsQueueReader : IQueueItemsReader<JobWithGroupModel>
{
    private readonly IPermanentLocksStorage _storage;

    public FreezePermanentLockedJobsQueueReader(IPermanentLocksStorage storage)
    {
        _storage = storage;
    }

    public Task ReadBatch(GetJobsRequest request, List<JobWithGroupModel> result)
    {
        return _storage.FreezePermanentLockedJobsFromTopOfQueue(request.QueueName, request.BatchSize, result);
    }
}