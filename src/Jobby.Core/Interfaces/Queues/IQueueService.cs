using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueService
{
    public Task WaitIfEmpty();
    public Task TakeBatchToProcessing(int batchSize, List<JobExecutionModel> result);
}