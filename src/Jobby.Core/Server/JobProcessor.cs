using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;

namespace Jobby.Core.Server;

public class JobProcessor : IJobProcessor
{
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly IJobsStorage _storage;
    private readonly SemaphoreSlim _semaphore;

    public JobProcessor(IJobExecutionScopeFactory scopeFactory, IJobsStorage storage, JobbySettings settings)
    {
        _scopeFactory = scopeFactory;
        _storage = storage;
        _semaphore = new SemaphoreSlim(settings.MaxDegreeOfParallelism);
    }

    public Task LockProcessingSlot()
    {
        return _semaphore.WaitAsync();
    }

    public void ReleaseProcessingSlot()
    {
        _semaphore.Release();
    }

    public void StartProcessing(JobModel job)
    {
        Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateJobExecutionScope();
                try
                {
                    var executor = scope.GetJobExecutor(job.JobName);
                    await executor.ExecuteAsync(job);
                }
                catch (Exception ex)
                {
                    // todo: do not use hardcoded retry policy
                    if (job.StartedCount >= 10)
                    {
                        await _storage.MarkFailedAsync(job.Id);
                    }
                    else
                    {
                        var sheduledStartTime = DateTime.UtcNow.AddMinutes(10);
                        await _storage.RescheduleAsync(job.Id, sheduledStartTime);
                    }
                    return;
                }

                //todo: log if error
                await _storage.MarkCompletedAsync(job.Id);
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }
}
