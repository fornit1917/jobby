using System.Threading.Channels;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

internal class BatchingJobCompletionService : IJobCompletionService
{
    private readonly IJobbyStorage _storage;
    private readonly JobbyServerSettings _settings;
    private readonly string _serverId;

    private readonly record struct QueueItem(TaskCompletionSource Tcs, JobExecutionModel Job);
    
    private readonly Channel<QueueItem> _chan;

    public BatchingJobCompletionService(IJobbyStorage storage, JobbyServerSettings settings, string serverId)
    {
        _storage = storage;
        _settings = settings;

        var chanOpts = new BoundedChannelOptions(capacity: _settings.MaxDegreeOfParallelism)
        {
            AllowSynchronousContinuations = false,
            Capacity = _settings.MaxDegreeOfParallelism,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _chan = Channel.CreateBounded<QueueItem>(chanOpts);
        Task.Run(Process);
        _serverId = serverId;
    }

    public Task CompleteJob(JobExecutionModel job)
    {
        var queueItem = new QueueItem
        {
            Job = job,
            Tcs = new TaskCompletionSource()
        };
        
        while (true)
        {
            var added = _chan.Writer.TryWrite(queueItem);
            if (added)
            {
                break;
            }
        }

        return queueItem.Tcs.Task;
    }

    private async Task Process()
    {
        var taskCompletionSources = new List<TaskCompletionSource>(capacity: _settings.MaxDegreeOfParallelism);
        var jobIds = new List<Guid>(capacity: _settings.MaxDegreeOfParallelism);
        var nextJobIds = new List<Guid>(capacity: _settings.MaxDegreeOfParallelism);

        while (true)
        {
            // Wait for items in the channel
            var hasItems = await _chan.Reader.WaitToReadAsync();
            if (!hasItems)
            {
                break;
            }

            // Get batch of items
            while (jobIds.Count < jobIds.Capacity && hasItems)
            {
                hasItems = _chan.Reader.TryRead(out var queueItem);
                if (!hasItems)
                {
                    break;
                }

                taskCompletionSources.Add(queueItem.Tcs);
                jobIds.Add(queueItem.Job.Id);
                if (queueItem.Job.NextJobId.HasValue)
                {
                    nextJobIds.Add(queueItem.Job.NextJobId.Value);
                }
            }

            if (jobIds.Count == 0)
            {
                continue;
            }

            // If batch is not empty - send bulk command to DB
            try
            {
                var completeJobsBatch = new CompleteJobsBatch
                {
                    ServerId = _serverId,
                    JobIds = jobIds,
                    NextJobIds = nextJobIds,
                };
                if (_settings.DeleteCompleted)
                {
                    await _storage.BulkDeleteProcessingJobsAsync(completeJobsBatch);
                }
                else
                {
                    await _storage.BulkUpdateProcessingJobsToCompletedAsync(completeJobsBatch);
                }
                
                SetCompletedForTasks(taskCompletionSources);
            }
            catch (Exception ex)
            {
                SetErrorForTasks(taskCompletionSources, ex);
            }

            // Clear buffers
            jobIds.Clear();
            nextJobIds.Clear();
            taskCompletionSources.Clear();
        }
    }

    private void SetCompletedForTasks(List<TaskCompletionSource> taskCompletionSources)
    {
        for (int i = 0; i < taskCompletionSources.Count; i++)
        {
            taskCompletionSources[i].SetResult();
        }
    }

    private void SetErrorForTasks(List<TaskCompletionSource> taskCompletionSources, Exception ex)
    {
        for (int i = 0; i < taskCompletionSources.Count; i++)
        {
            taskCompletionSources[i].SetException(ex);
        }
    }

    public void Dispose()
    {
        _chan.Writer.Complete();
    }
}
