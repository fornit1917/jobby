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
        Task.Run(Poll);
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
}
