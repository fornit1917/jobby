using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobCompletionService
{
    Task CompleteJob(JobExecutionModel job);
}
