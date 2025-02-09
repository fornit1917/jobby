using Jobby.Abstractions.CommonServices;
using Jobby.Abstractions.Models;
using Jobby.Abstractions.Server;

namespace Jobby.Core.Server;

public class JobsPollingService : IJobsPollingService
{
    private readonly IJobsStorage _storage;
    private readonly IJobProcessor _jobProcessor;
    private readonly JobbySettings _settings;

    private bool _running;

    public JobsPollingService(IJobsStorage storage, IJobProcessor jobProcessor, JobbySettings settings)
    {
        _storage = storage;
        _jobProcessor = jobProcessor;
        _settings = settings;
    }

    public void StartBackgroundService()
    {
        _running = true;
        if (_settings.UseBatches)
        {
            Task.Run(PollByBatches);
        }
        else
        {
            Task.Run(Poll);
        }
    }

    public void SendStopSignal()
    {
        _running = false;
    }

    private async Task Poll()
    {
        while (_running)
        {
            JobModel? job = null;
            await _jobProcessor.LockProcessingSlot();
            try
            {
                job = await _storage.TakeToProcessingAsync();
            }
            catch 
            { 
                _jobProcessor.ReleaseProcessingSlot();
                // todo: log error
                await Task.Delay(_settings.DbErrorPauseMs);
            }

            
            if (job == null)
            {
                _jobProcessor.ReleaseProcessingSlot();
                if (_running)
                {
                    await Task.Delay(_settings.PollingIntervalMs);
                }
            }
            else
            {
                _jobProcessor.StartProcessing(job);
            }
        }
    }

    private async Task PollByBatches()
    {
        var jobs = new List<JobModel>(capacity: _settings.MaxDegreeOfParallelism);
        while (_running) 
        {
            await _jobProcessor.LockProcessingSlot();
            var maxBatchSize = _jobProcessor.GetFreeProcessingSlotsCount() + 1;

            try
            {
                await _storage.TakeBatchToProcessingAsync(maxBatchSize, jobs);
            }
            catch (Exception ex)
            {
                _jobProcessor.ReleaseProcessingSlot();
                // todo: log error
                await Task.Delay(_settings.DbErrorPauseMs);
            }

            if (jobs.Count == 0)
            {
                _jobProcessor.ReleaseProcessingSlot();
                if (_running)
                {
                    await Task.Delay(_settings.PollingIntervalMs);
                }
            }
            else
            {
                var actualBatchSize = jobs.Count;
                for (int i = 1; i < actualBatchSize; i++)
                {
                    await _jobProcessor.LockProcessingSlot();
                }

                _jobProcessor.StartProcessing(jobs);
            }
        }
    }
}
