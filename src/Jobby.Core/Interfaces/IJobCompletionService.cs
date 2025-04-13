namespace Jobby.Core.Interfaces;

internal interface IJobCompletionService
{
    Task CompleteJob(Guid jobId, Guid? nextJobId);
}
