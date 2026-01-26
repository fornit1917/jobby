using Jobby.Core.Interfaces.Queues;
using Jobby.Core.Models;

namespace Jobby.Core.Services.Queues;

internal class QueueNameAssignor : IQueueNameAssignor
{
    private readonly IReadOnlyDictionary<string, string> _queueNameByJobName;
    private readonly string? _queueNameForRecurrent;

    public QueueNameAssignor(IReadOnlyDictionary<string, string> queueNameByJobName, string? queueNameForRecurrent)
    {
        _queueNameByJobName = queueNameByJobName;
        _queueNameForRecurrent = queueNameForRecurrent;
    }

    public string GetQueueName(string jobName, JobCreationOptions jobCreationOptions)
    {
        return jobCreationOptions.QueueName
               ?? _queueNameByJobName.GetValueOrDefault(jobName)
               ?? QueueSettings.DefaultQueueName;
    }

    public string GetQueueNameForRecurrent(string jobName, JobCreationOptions jobCreationOptions)
    {
        return jobCreationOptions.QueueName
               ?? _queueNameByJobName.GetValueOrDefault(jobName)
               ?? _queueNameForRecurrent
               ?? QueueSettings.DefaultQueueName;
    }
}