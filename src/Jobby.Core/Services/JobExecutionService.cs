using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Jobby.Core.Services;

internal class JobExecutionService : IJobExecutionService
{
    private readonly IJobExecutionScopeFactory _scopeFactory;
    private readonly IJobsRegistry _jobsRegistry;
    private readonly IRetryPolicyService _retryPolicyService;
    private readonly IJobParamSerializer _serializer;
    private readonly IJobPostProcessingService _postProcessingService;
    private readonly ILogger<JobExecutionService> _logger;

    public JobExecutionService(IJobExecutionScopeFactory scopeFactory,
        IJobsRegistry jobsRegistry,
        IRetryPolicyService retryPolicyService,
        IJobParamSerializer serializer,
        IJobPostProcessingService postProcessingService,
        ILogger<JobExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _jobsRegistry = jobsRegistry;
        _retryPolicyService = retryPolicyService;
        _serializer = serializer;
        _postProcessingService = postProcessingService;
        _logger = logger;
    }

    public async Task ExecuteJob(JobExecutionModel job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateJobExecutionScope();
        var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
        Exception? thrownException = null;
        try
        {
            var execMetadata = _jobsRegistry.GetJobExecutionMetadata(job.JobName);
            if (execMetadata == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var handlerInstance = scope.GetService(execMetadata.HandlerType);
            if (handlerInstance == null)
            {
                throw new InvalidJobHandlerException($"Could not create instance of handler with type {execMetadata.HandlerType}");
            }

            var command = _serializer.DeserializeJobParam(job.JobParam, execMetadata.CommandType);
            if (command == null)
            {
                throw new InvalidJobHandlerException($"Could not deserialize job parameter with type {execMetadata.CommandType}");
            }

            var ctx = new JobExecutionContext
            {
                JobName = job.JobName,
                StartedCount = job.StartedCount,
                IsLastAttempt = retryPolicy.IsLastAttempt(job),
                CancellationToken = cancellationToken,
            };

            object? result = null;
            try
            {
                result = execMetadata.ExecMethod.Invoke(handlerInstance, [command, ctx]);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                thrownException = e.InnerException;
            }
            
            if (result != null && result is Task)
            {
                await(Task)result;
            }
        }
        catch (Exception e)
        {
            thrownException = e;
        }

        if (thrownException == null)
        {
            // completed
            if (job.IsRecurrent)
            {
                await _postProcessingService.RescheduleRecurrent(job, error: null);
            }
            else
            {
                await _postProcessingService.HandleCompleted(job);
            }
        }
        else
        {
            _logger.LogError(thrownException, "Error executing job, jobName = {JobName}, id = {JobId}", job.JobName, job.Id);

            var error = thrownException.ToString();

            if (job.IsRecurrent)
            {
                await _postProcessingService.RescheduleRecurrent(job, error);
            }
            else
            {
                await _postProcessingService.HandleFailed(job, retryPolicy, error);
            }
        }

    }

    public void Dispose()
    {
        _postProcessingService.Dispose();
    }
}
