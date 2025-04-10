namespace Jobby.Core.Interfaces;

internal interface IJobCompletingService
{
    Task CompleteJob(Guid jobId, Guid? nextJobId);
}
