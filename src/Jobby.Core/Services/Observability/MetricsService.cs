using Jobby.Core.Interfaces.Observability;
using Jobby.Core.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Jobby.Core.Services.Observability;

internal class MetricsService : IMetricsService
{
    private readonly Meter _meter;
    private readonly Counter<long> _startedCounter;
    private readonly Counter<long> _completedCounter;
    private readonly Counter<long> _retriedCounter;
    private readonly Counter<long> _failedCounter;
    private readonly Histogram<double> _durationHistogram;

    public static readonly MetricsService Instance = new MetricsService();

    public MetricsService()
    {
        _meter = new Meter(JobbyMeterNames.JobsExecution, JobbyMeterNames.Version);
        _startedCounter = _meter.CreateCounter<long>("jobby.inst.jobs.started", "ea", "Number of jobs runs");
        _completedCounter = _meter.CreateCounter<long>("jobby.inst.jobs.completed", "ea", "Number of successfully completed jobs");
        _retriedCounter = _meter.CreateCounter<long>("jobby.inst.jobs.retried", "ea", "Number of jobs scheduled for retry after an error");
        _failedCounter = _meter.CreateCounter<long>("jobby.inst.jobs.failed", "ea", "Number of failed last job attempts and failed recurrent jobs runs");
        _durationHistogram = _meter.CreateHistogram<double>("jobby.inst.jobs.duration", "ms", "Duration of jobs execution");
    }

    public void AddStarted(JobExecutionContext ctx)
    {
        var tagList = ContextToTags(ctx);
        _startedCounter.Add(1, tagList);
    }

    public void AddCompleted(JobExecutionContext ctx)
    {
        var tagList = ContextToTags(ctx);
        _completedCounter.Add(1, tagList);
    }

    public void AddRetried(JobExecutionContext ctx)
    {
        var tagList = ContextToTags(ctx);
        _retriedCounter.Add(1, tagList);
    }

    public void AddFailed(JobExecutionContext ctx)
    {
        var tagList = ContextToTags(ctx);
        _failedCounter.Add(1, tagList);
    }

    public void AddDuration(JobExecutionContext ctx, double duration)
    {
        var tagList = ContextToTags(ctx);
        _durationHistogram.Record(duration, tagList);
    }

    private TagList ContextToTags(JobExecutionContext ctx)
    {
        var tagList = new TagList()
        {
            new KeyValuePair<string, object?>("job_name", ctx.JobName),
        };
        return tagList;
    }
}
