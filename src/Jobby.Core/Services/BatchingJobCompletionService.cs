using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using System.Threading.Channels;

namespace Jobby.Core.Services;

internal class BatchingJobCompletionService : IJobCompletionService, IDisposable
{
    private readonly IJobbyStorage _storage;
    private readonly JobbyServerSettings _settings;
    private readonly string _serverId;

    private readonly record struct QueueItem(TaskCompletionSource Tcs, Guid JobId, Guid? NextJobId, string? SequenceId);
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

    public Task CompleteJob(Guid jobId, Guid? nextJobId, string? sequenceId)
    {
        var queueItem = new QueueItem
        {
            JobId = jobId,
            NextJobId = nextJobId,
            Tcs = new TaskCompletionSource(),
            SequenceId = sequenceId
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
        // Separate buffers for sequence vs non-sequence jobs
        var sequenceJobIds = new List<Guid>(capacity: _settings.MaxDegreeOfParallelism);
        var sequenceIds = new List<string>(capacity: _settings.MaxDegreeOfParallelism);
        var nonSequenceJobIds = new List<Guid>(capacity: _settings.MaxDegreeOfParallelism);
        var nextJobIds = new List<Guid>(capacity: _settings.MaxDegreeOfParallelism);
        var taskCompletionSources = new List<TaskCompletionSource>(capacity: _settings.MaxDegreeOfParallelism);

        while (true)
        {
            // Wait for items in the channel
            var hasItems = await _chan.Reader.WaitToReadAsync();
            if (!hasItems)
            {
                break;
            }

            // Get batch of items
            var totalCount = sequenceJobIds.Count + nonSequenceJobIds.Count;
            while (totalCount < _settings.MaxDegreeOfParallelism && hasItems)
            {
                hasItems = _chan.Reader.TryRead(out var queueItem);
                if (!hasItems)
                {
                    break;
                }

                // Validate mutual exclusivity (mirrors single-job validation in storage layer)
                if (queueItem is { SequenceId: not null, NextJobId: not null })
                {
                    queueItem.Tcs.SetException(new InvalidOperationException(
                        $"Job {queueItem.JobId} cannot have both sequenceId and nextJobId set. " +
                        "These are mutually exclusive sequencing mechanisms."));
                    continue;
                }

                if (queueItem.SequenceId != null)
                {
                    sequenceJobIds.Add(queueItem.JobId);
                    sequenceIds.Add(queueItem.SequenceId);
                }
                else
                {
                    nonSequenceJobIds.Add(queueItem.JobId);
                    if (queueItem.NextJobId.HasValue)
                    {
                        nextJobIds.Add(queueItem.NextJobId.Value);
                    }
                }
                taskCompletionSources.Add(queueItem.Tcs);
                totalCount = sequenceJobIds.Count + nonSequenceJobIds.Count;
            }

            if (totalCount == 0)
            {
                continue;
            }

            // Make separate storage calls for each batch type
            // NOTE: If one call fails after the other succeeds, all tasks get the error.
            // This is acceptable because:
            // - Storage updates are idempotent (re-completing an already-completed job is a no-op)
            // - The retry queue will re-attempt failed tasks, and no-op updates won't cause issues
            // - Splitting error handling per-group would add complexity without much benefit
            try
            {
                if (_settings.DeleteCompleted)
                {
                    if (nonSequenceJobIds.Count > 0)
                    {
                        var nonSeqJobs = new ProcessingJobsList(nonSequenceJobIds, _serverId);
                        await _storage.BulkDeleteProcessingJobsAsync(nonSeqJobs, nextJobIds, sequenceIds: null);
                    }
                    if (sequenceJobIds.Count > 0)
                    {
                        var seqJobs = new ProcessingJobsList(sequenceJobIds, _serverId);
                        await _storage.BulkDeleteProcessingJobsAsync(seqJobs, nextJobIds: null, sequenceIds);
                    }
                }
                else
                {
                    if (nonSequenceJobIds.Count > 0)
                    {
                        var nonSeqJobs = new ProcessingJobsList(nonSequenceJobIds, _serverId);
                        await _storage.BulkUpdateProcessingJobsToCompletedAsync(nonSeqJobs, nextJobIds, sequenceIds: new List<string>());
                    }
                    if (sequenceJobIds.Count > 0)
                    {
                        var seqJobs = new ProcessingJobsList(sequenceJobIds, _serverId);
                        await _storage.BulkUpdateProcessingJobsToCompletedAsync(seqJobs, new List<Guid>(), sequenceIds);
                    }
                }

                SetCompletedForTasks(taskCompletionSources);
            }
            catch (Exception ex)
            {
                SetErrorForTasks(taskCompletionSources, ex);
            }

            // Clear all buffers
            sequenceJobIds.Clear();
            sequenceIds.Clear();
            nonSequenceJobIds.Clear();
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
