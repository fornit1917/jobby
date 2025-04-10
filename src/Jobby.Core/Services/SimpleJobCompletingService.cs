using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class SimpleJobCompletingService : IJobCompletingService
{
    private readonly IJobsStorage _storage;
    private readonly bool _deleteCompleted;

    public SimpleJobCompletingService(IJobsStorage storage, bool deleteCompletedJobs)
    {
        _storage = storage;
        _deleteCompleted = deleteCompletedJobs;
    }

    public Task CompleteJob(Guid jobId, Guid? nextJobId)
    {
        return _deleteCompleted
            ? _storage.DeleteAsync(jobId, nextJobId)
            : _storage.MarkCompletedAsync(jobId, nextJobId);
    }
}
