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
        using var activity = StartActivity(ctx.JobName, ctx.Traceparent, ctx.TracingMode);
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

    private static Activity? StartActivity(string jobName, string? traceparent, JobbyTracingMode tracingMode)
    {
        // the job execution span is named after the job; job names are compile-time constants,
        // so the span name stays low-cardinality as OTel conventions require
        var activityName = string.IsNullOrEmpty(jobName) ? "JobExecution" : jobName;

        if (!string.IsNullOrEmpty(traceparent)
            && ActivityContext.TryParse(traceparent, traceState: null, out var parentContext))
        {
            switch (tracingMode)
            {
                case JobbyTracingMode.RestoreTraceId:
                    // continue the captured trace; sampling flags of the original trace are preserved
                    return JobbyActivitySource.StartActivity(activityName, ActivityKind.Internal, parentContext);

                case JobbyTracingMode.LinkTraceId:
                    // start an independent trace and reference the captured one via a link
                    return JobbyActivitySource.StartActivity(
                        activityName,
                        ActivityKind.Internal,
                        parentContext: default,
                        tags: null,
                        links: new[] { new ActivityLink(parentContext) });
            }
        }

        // CreateNewTraceId mode, missing or invalid traceparent — start a new trace
        return JobbyActivitySource.StartActivity(activityName);
    }
}
