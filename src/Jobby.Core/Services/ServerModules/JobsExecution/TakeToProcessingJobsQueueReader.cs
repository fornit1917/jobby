using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

public class TakeToProcessingJobsQueueReader : IQueueItemsReader<JobExecutionModel>
{
    private readonly IJobbyStorage _storage;

    public TakeToProcessingJobsQueueReader(IJobbyStorage storage)
    {
        _storage = storage;
    }

    public Task ReadBatch(GetJobsRequest request, List<JobExecutionModel> result)
    {
        return _storage.TakeBatchToProcessingAsync(request, result);
    }
}