using Jobby.Core.Interfaces.Queues;

namespace Jobby.Core.Services.Queues;

internal class QueueServiceFactory : IQueueServiceFactory
{
    public static readonly IQueueServiceFactory Instance = new QueueServiceFactory();
    
    public IQueueService<T> Create<T>(IQueueItemsReader<T> queueItemsReader, QueueServiceConfig config, string serverId)
    {
        return config.Queues.Count > 1 
            ? new MultiQueueService<T>(queueItemsReader, TimerService.Instance, config, serverId)
            : new SingleQueueService<T>(queueItemsReader, config, serverId);
    }
}