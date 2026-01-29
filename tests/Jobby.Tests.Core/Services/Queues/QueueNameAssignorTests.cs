using Jobby.Core.Models;
using Jobby.Core.Services.Queues;

namespace Jobby.Tests.Core.Services.Queues;

public class QueueNameAssignorTests
{
    [Theory]
    [InlineData("instance", "job", "instance")]
    [InlineData(null, "job", "job")]
    [InlineData(null, null, QueueSettings.DefaultQueueName)]
    public void GetQueueName_ReturnsExpectedResult(string? instanceLevelQueueName, string? jobTypeLevelQueueName, string? expected)
    {
        var queueNameByJobName = new Dictionary<string, string>
        {
            ["OtherJob"] = "other",
        };
        if (jobTypeLevelQueueName != null)
            queueNameByJobName["MyJobName"] =  jobTypeLevelQueueName;

        var queueNameAssignor = new QueueNameAssignor(queueNameByJobName, "recurrent");
        
        var queue = queueNameAssignor.GetQueueName("MyJobName", new JobOpts
        {
            QueueName = instanceLevelQueueName
        });
        
        Assert.Equal(expected, queue);
    }

    [Theory]
    [InlineData("instance", "job", "recurrent", "instance")]
    [InlineData(null, "job", "recurrent", "job")]
    [InlineData(null, null, "recurrent", "recurrent")]
    [InlineData(null, null, null, QueueSettings.DefaultQueueName)]
    public void GetQueueNameForRecurrent_ReturnsExpectedResult(string? instanceLevelQueueName,
        string? jobTypeLevelQueueName,
        string? defaultForRecurrentQueueName,
        string? expected)
    {
        var queueNameByJobName = new Dictionary<string, string>
        {
            ["OtherJob"] = "other",
        };
        if (jobTypeLevelQueueName != null)
            queueNameByJobName["MyJobName"] =  jobTypeLevelQueueName;
        
        var queueNameAssignor = new QueueNameAssignor(queueNameByJobName, defaultForRecurrentQueueName);
        
        var queue = queueNameAssignor.GetQueueNameForRecurrent("MyJobName", new RecurrentJobOpts
        {
            QueueName = instanceLevelQueueName
        });
        
        Assert.Equal(expected, queue);
    }
}