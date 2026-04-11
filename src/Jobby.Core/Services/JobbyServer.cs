using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.ServerModules;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Interfaces.ServerModules.PermanentLockedGroupsCheck;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services;

internal class JobbyServer : IJobbyServer
{
    private readonly IAvailabilityCheckServerModule _availabilityCheckServerModule;
    private readonly IJobsExecutionServerModule _jobsExecutionServerModule;
    private readonly IPermanentLocksCheckServerModule _permanentLocksCheckServerModule;
    private readonly ILogger<JobbyServer> _logger;
    
    public string ServerId { get; }

    public JobbyServer(IAvailabilityCheckServerModule availabilityCheckServerModule,
        IJobsExecutionServerModule jobsExecutionServerModule,
        IPermanentLocksCheckServerModule permanentLocksCheckServerModule,
        ILogger<JobbyServer> logger,
        string serverId)
    {
        _availabilityCheckServerModule = availabilityCheckServerModule;
        _jobsExecutionServerModule = jobsExecutionServerModule;
        _permanentLocksCheckServerModule = permanentLocksCheckServerModule;
        _logger = logger;
        
        ServerId = serverId;
    }

    public void StartBackgroundService()
    {
        _logger.LogInformation("Jobby server is running, serverId = {ServerId}", ServerId);
        _availabilityCheckServerModule.Start();
        _jobsExecutionServerModule.Start();
        _permanentLocksCheckServerModule.Start();
    }

    public void SendStopSignal()
    {
        _logger.LogInformation("Jobby server received stop signal, serverId = {ServerId}", ServerId);
        _jobsExecutionServerModule.SendStopSignal();
        _availabilityCheckServerModule.SendStopSignal();
        _permanentLocksCheckServerModule.SendStopSignal();
    }

    public bool HasInProgressJobs()
    {
        return _jobsExecutionServerModule.HasInProgressJobs();
    }
}
