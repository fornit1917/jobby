namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueServiceFactory
{
    IQueueService<T> Create<T>(IQueueItemsReader<T> queueItemsReader, QueueServiceConfig config, string serverId);
}