using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class SimpleJobCompletionService : IJobCompletionService
{
    private readonly IJobsStorage _storage;
    private readonly bool _deleteCompleted;

    public SimpleJobCompletionService(IJobsStorage storage, bool deleteCompletedJobs)
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
