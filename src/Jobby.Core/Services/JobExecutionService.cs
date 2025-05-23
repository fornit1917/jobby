using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Microsoft.Extensions.Logging;

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

    public async Task ExecuteCommand(Job job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateJobExecutionScope();
        var retryPolicy = _retryPolicyService.GetRetryPolicy(job);
        var completed = false;
        try
        {
            var execMetadata = _jobsRegistry.GetCommandExecutionMetadata(job.JobName);
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

            var ctx = new CommandExecutionContext
            {
                JobName = job.JobName,
                StartedCount = job.StartedCount,
                IsLastAttempt = retryPolicy.IsLastAttempt(job),
                CancellationToken = cancellationToken,
            };
            var result = execMetadata.ExecMethod.Invoke(handlerInstance, [command, ctx]);
            if (result is Task)
            {
                await (Task)result;
            }

            completed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing job, jobName = {job.JobName}, id = {job.Id}");
            await _postProcessingService.HandleFailedAsync(job, retryPolicy);
        }

        if (completed)
        {
            await _postProcessingService.HandleCompletedAsync(job);
        }
    }

    public async Task ExecuteRecurrent(Job job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job.Cron, nameof(job.Cron));

        using var scope = _scopeFactory.CreateJobExecutionScope();
        try
        {
            var execMetadata = _jobsRegistry.GetRecurrentJobExecutionMetadata(job.JobName);
            if (execMetadata == null)
            {
                throw new InvalidJobHandlerException($"Job {job.JobName} does not have suitable handler");
            }

            var handlerInstance = scope.GetService(execMetadata.HandlerType);
            if (handlerInstance == null)
            {
                throw new InvalidJobHandlerException($"Could not create instance of handler with type {execMetadata.HandlerType}");
            }

            var ctx = new RecurrentJobExecutionContext
            {
                JobName = job.JobName,
                CancellationToken = cancellationToken,
            };
            var result = execMetadata.ExecMethod.Invoke(handlerInstance, [ctx]);
            if (result is Task)
            {
                await (Task)result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing recurrent job, jobName = {job.JobName}, id = {job.Id}");
        }
        finally
        {
            await _postProcessingService.RescheduleRecurrentAsync(job);
        }
    }

    public void Dispose()
    {
        _postProcessingService.Dispose();
    }
}
