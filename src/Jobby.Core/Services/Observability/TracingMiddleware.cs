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
        try
        {
            await handler.ExecuteAsync(command, ctx);
        }
        catch (Exception e)
        {
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error, e.Message);
                // Activity.AddException appeared only in .NET 9, add the exception event manually by OTel conventions
                activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    ["exception.type"] = e.GetType().FullName,
                    ["exception.message"] = e.Message,
                    ["exception.stacktrace"] = e.ToString(),
                }));
            }
            throw;
        }
    }
}
