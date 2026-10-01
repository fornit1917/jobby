using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Interfaces.ServerModules.JobsExecution;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services.ServerModules.JobsExecution;

internal class JobExecutionService : IJobExecutionService
{
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly IJobsRegistry _jobsRegistry;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IJobParamSerializer _serializer;
    private readonly IPipelineBuilder _pipelineBuilder;
    private readonly IJobPostProcessingService _postProcessingService;
    private readonly ILogger<JobExecutionService> _logger;

    public JobExecutionService(IJobExecutionScopeFactory scopeFactory,
        IJobsRegistry jobsRegistry,
        IRetryPolicyService retryPolicyService,
        IJobParamSerializer serializer,
        IPipelineBuilder pipelineBuilder,
        IJobPostProcessingService postProcessingService,
        ILogger<JobExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobsRegistry = jobsRegistry;
        _retryPolicyService = retryPolicyService;
        _serializer = serializer;
        _pipelineBuilder = pipelineBuilder;
        _postProcessingService = postProcessingService;
        _logger = logger;
    }

    public async Task ExecuteJob(JobExecutionModel job, CancellationToken cancellationToken)
    {
        var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
        string? error = null;
        var completed = false;
        
        try
        {
            using var scope = _scopeFactory.CreateJobExecutionScope();
            
            var jobExecutor = _jobsRegistry.GetJobExecutor(job.JobName);
            if (jobExecutor == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var ctx = new JobExecutionContext
            {
                CancellationToken = cancellationToken,
                IsRecurrent = job.IsRecurrent,
                IsLastAttempt = !job.IsRecurrent && retryPolicy.IsLastAttempt(job),
                JobName = job.JobName,
                StartedCount = job.StartedCount,
            };

            await jobExecutor.Execute(job, ctx, scope, _serializer, _pipelineBuilder);
            completed = true;
        }
        catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        {
            // In this case job will be restarted on another instance by heartbeat process if can_be_restarted=true
            // So we just write a log and keep Processing status
            
            _logger.LogWarning(e, 
                "Job execution was interrupted due to a server shutdown, jobName = {JobName}, id = {JobId}. The job will be restarted on another instance if permitted.",
                job.JobName, job.Id);
            
            // todo: reschedule job if can_be_restarted=true right here
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error executing job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);
            error = e.ToString();
        }

        if (job.IsRecurrent)
        {
            await _postProcessingService.RescheduleRecurrent(job, error);
        }
        else
        {
            if (error is null && completed)
                await _postProcessingService.HandleCompleted(job);
            else if (error is not null)
                await _postProcessingService.HandleFailed(job, retryPolicy, error);
        }
    }
}
