using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueItemsReader<T>
{
    Task ReadBatch(GetJobsRequest request, List<T> result);
}