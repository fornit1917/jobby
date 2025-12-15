using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Interfaces.Observability;
using Jobby.Core.Models;
using System.Diagnostics;

namespace Jobby.Core.Services.Observability;

internal class MetricsMiddleware : IJobbyMiddleware
{
    private readonly IMetricsService _metrics;

    public MetricsMiddleware(IMetricsService metrics)
    {
        _metrics = metrics;
    }

    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler) where TCommand : IJobCommand
    {
        _metrics.AddStarted(ctx);

        var staringTs = Stopwatch.GetTimestamp();
        try
        {
            await handler.ExecuteAsync(command, ctx);
            _metrics.AddCompleted(ctx);
        }
        catch (Exception)
        {
            if (ctx.IsRecurrent || ctx.IsLastAttempt)
            {
                _metrics.AddFailed(ctx);
            }
            else
            {
                _metrics.AddRetried(ctx);
            }
            
            throw;
        }
        finally
        {
            var executionTime = Stopwatch.GetElapsedTime(staringTs);
            _metrics.AddDuration(ctx, executionTime.TotalMilliseconds);
        }
    }
}
