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

    public string GetQueueName(string jobName, JobOpts opts)
    {
        return opts.QueueName
               ?? _queueNameByJobName.GetValueOrDefault(jobName)
               ?? QueueSettings.DefaultQueueName;
    }

    public string GetQueueNameForRecurrent(string jobName, RecurrentJobOpts opts)
    {
        return opts.QueueName
               ?? _queueNameByJobName.GetValueOrDefault(jobName)
               ?? _queueNameForRecurrent
               ?? QueueSettings.DefaultQueueName;
    }
}