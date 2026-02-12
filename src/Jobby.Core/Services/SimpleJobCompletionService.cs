using Jobby.Core.Interfaces;
using Jobby.Core.Models;

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

    public Task CompleteJob(JobExecutionModel job)
    {
        return _deleteCompleted
            ? _storage.DeleteProcessingJobAsync(job)
            : _storage.UpdateProcessingJobToCompletedAsync(job);
    }
}
