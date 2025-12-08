using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

namespace Jobby.Core.Services;

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
        using var scope = _scopeFactory.CreateJobExecutionScope();
        var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
        string? error = null;
        try
        {
            var jobExecutor = _jobsRegistry.GetJobExecutor(job.JobName);
            if (jobExecutor == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var ctx = new JobExecutionContext
            {
                CancellationToken = cancellationToken,
                IsRecurrent = job.IsRecurrent,
                IsLastAttempt = job.IsRecurrent ? false : retryPolicy.IsLastAttempt(job),
                JobName = job.JobName,
                StartedCount = job.StartedCount,
            };

            await jobExecutor.Execute(job, ctx, scope, _serializer, _pipelineBuilder);
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
            if (error is null)
                await _postProcessingService.HandleCompleted(job);
            else
                await _postProcessingService.HandleFailed(job, retryPolicy, error);
        }
    }

    public void Dispose()
    {
        _postProcessingService.Dispose();
    }
}
