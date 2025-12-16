using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using System.Diagnostics;

namespace Jobby.Core.Services.Observability;

internal class TracingMiddleware : IJobbyMiddleware
{
    private static readonly ActivitySource JobbyActivitySource = new ActivitySource(JobbyActivitySourceNames.JobsExecution);

    public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler)
        where TCommand : IJobCommand
    {
        using var activity = JobbyActivitySource.StartActivity("JobExecution");
        activity?.SetTag("job_name", ctx.JobName);
        await handler.ExecuteAsync(command, ctx);
    }
}
