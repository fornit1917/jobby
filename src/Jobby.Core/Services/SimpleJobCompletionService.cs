using Jobby.Core.Interfaces;

namespace Jobby.Core.Services;

internal class SimpleJobCompletionService : IJobCompletionService
{
    private readonly IJobbyStorage _storage;
    private readonly bool _deleteCompleted;

    public SimpleJobCompletionService(IJobbyStorage storage, bool deleteCompletedJobs)
    {
        _storage = storage;
        _deleteCompleted = deleteCompletedJobs;
    }

    public Task CompleteJob(Guid jobId, Guid? nextJobId)
    {
        return _deleteCompleted
            ? _storage.DeleteProcessingJobAsync(jobId, nextJobId)
            : _storage.UpdateProcessingJobToCompletedAsync(jobId, nextJobId);
    }
}
