using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;
using Jobby.Core.Services.Queues;
using Moq;

namespace Jobby.Tests.Core.Services.Queues;

public class QueueServiceFactoryTests
{
    private readonly Mock<IQueueItemsReader<JobExecutionModel>> _queueItemsReaderMock = new();
    private const string ServerId = "ServerId";
    
    [Fact]
    public void EmptyQueueList_Throws()
    {
        var config = new QueueServiceConfig();
        var factory = new QueueServiceFactory();
        
        Assert.Throws<ArgumentException>(() => factory.Create(_queueItemsReaderMock.Object, config, ServerId));
    }

    [Fact]
    public void OneQueue_CreatesSingleQueueService()
    {
        var config = new QueueServiceConfig
        {
            Queues =
            [
                new()
                {
                    QueueName = "q1",
                }
            ]
        };
        var factory = new QueueServiceFactory();
        
        var service = factory.Create(_queueItemsReaderMock.Object, config, ServerId);
        
        Assert.True(service is SingleQueueService<JobExecutionModel>);
    }

    [Fact]
    public void TwoQueues_CreatesMultiQueueService()
    {
        var config = new QueueServiceConfig
        {
            Queues =
            [
                new()
                {
                    QueueName = "q1",
                },
                new()
                {
                    QueueName = "q2",
                }
            ]
        };
        var factory = new QueueServiceFactory();
        
        var service = factory.Create(_queueItemsReaderMock.Object, config, ServerId);
        
        Assert.True(service is MultiQueueService<JobExecutionModel>);
    }
}