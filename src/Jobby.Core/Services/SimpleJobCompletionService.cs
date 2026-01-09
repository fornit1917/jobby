using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

internal class SimpleJobCompletionService : IJobCompletionService
{
    private readonly IJobbyStorage _storage;
    private readonly bool _deleteCompleted;
    private readonly string _serverId;

    public SimpleJobCompletionService(IJobbyStorage storage, bool deleteCompletedJobs, string serverId)
    {
        _storage = storage;
        _deleteCompleted = deleteCompletedJobs;
        _serverId = serverId;
    }

    public Task CompleteJob(Guid jobId, Guid? nextJobId, string? sequenceId)
    {
        return _deleteCompleted
            ? _storage.DeleteProcessingJobAsync(new ProcessingJob(jobId, _serverId), nextJobId, sequenceId)
            : _storage.UpdateProcessingJobToCompletedAsync(new ProcessingJob(jobId, _serverId), nextJobId, sequenceId);
    }
}
