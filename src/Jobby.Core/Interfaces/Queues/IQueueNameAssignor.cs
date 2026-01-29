using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueNameAssignor
{
    string GetQueueName(string jobName, JobOpts opts);
    string GetQueueNameForRecurrent(string jobName, RecurrentJobOpts opts);
}