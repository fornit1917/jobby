using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.JobsMiddlewares;

public class JobLoggingMiddleware : IJobbyMiddleware
{
    private readonly ILogger<JobLoggingMiddleware> _logger;

    public JobLoggingMiddleware(ILogger<JobLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler)
        where TCommand : IJobCommand
    {
        try
        {
            _logger.LogInformation($"Job {ctx.JobName} started");
            await handler.ExecuteAsync(command, ctx);
            _logger.LogInformation($"Job {ctx.JobName} completed");
        }
        catch (Exception ex)
        { 
            if (ctx.IsLastAttempt)
            {
                _logger.LogError(ex, $"Job {ctx.JobName} failed");
            }
            else
            {
                _logger.LogWarning(ex, $"Job {ctx.JobName} failed and will be restarted");
            }
            throw;
        }
    }
}
