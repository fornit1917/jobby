using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueService<T>
{
    public int GetWaitingIntervalMs();
    public Task ReadBatch(int batchSize, List<T> result);
}