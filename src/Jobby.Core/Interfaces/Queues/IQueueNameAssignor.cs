using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Queues;

internal interface IQueueNameAssignor
{
    string GetQueueName(string jobName, JobCreationOptions jobCreationOptions);
    string GetQueueNameForRecurrent(string jobName, JobCreationOptions jobCreationOptions);
}