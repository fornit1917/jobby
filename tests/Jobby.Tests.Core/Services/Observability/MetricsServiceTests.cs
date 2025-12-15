using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using System.Diagnostics.Metrics;

namespace Jobby.Tests.Core.Services.Observability;

public sealed class MetricsServiceTests : IDisposable
{
    private long _longVal = 0;
    private double _doubleVal = -1000000;
    private readonly List<KeyValuePair<string, object?>[]> _tags;

    private readonly MeterListener _listener;

    private readonly JobExecutionContext Ctx = new JobExecutionContext
    {
        CancellationToken = CancellationToken.None,
        IsLastAttempt = false,
        IsRecurrent = false,
        StartedCount = 1,
        JobName = "JobName"
    };

    public MetricsServiceTests()
    {
        _tags = new List<KeyValuePair<string, object?>[]>();
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name.StartsWith("jobby."))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
    }

    [Fact]
    public void AddStarted_IncrementsStarted()
    {
        ListenLongMetric("jobby.inst.jobs.started");
        
        MetricsService.Instance.AddStarted(Ctx);

        Assert.Equal(1, _longVal);
        Assert.Single(_tags);
        Assert.Contains(_tags[0], x => x.Key == "job_name" && x.Value as string == Ctx.JobName);
    }

    [Fact]
    public void AddCompleted_IncrementsCompleted()
    {
        ListenLongMetric("jobby.inst.jobs.completed");

        MetricsService.Instance.AddCompleted(Ctx);

        Assert.Equal(1, _longVal);
        Assert.Single(_tags);
        Assert.Contains(_tags[0], x => x.Key == "job_name" && x.Value as string == Ctx.JobName);
    }

    [Fact]
    public void AddRetried_IncrementsRetried()
    {
        ListenLongMetric("jobby.inst.jobs.retried");

        MetricsService.Instance.AddRetried(Ctx);

        Assert.Equal(1, _longVal);
        Assert.Single(_tags);
        Assert.Contains(_tags[0], x => x.Key == "job_name" && x.Value as string == Ctx.JobName);
    }

    [Fact]
    public void AddFailed_IncrementsFailed()
    {
        ListenLongMetric("jobby.inst.jobs.failed");

        MetricsService.Instance.AddFailed(Ctx);

        Assert.Equal(1, _longVal);
        Assert.Single(_tags);
        Assert.Contains(_tags[0], x => x.Key == "job_name" && x.Value as string == Ctx.JobName);
    }

    [Fact]
    public void AddDuration_AddsDuration()
    {
        ListenDoubleMetric("jobby.inst.jobs.duration");

        double duration = 10;
        MetricsService.Instance.AddDuration(Ctx, duration);

        Assert.Equal(duration, _doubleVal);
        Assert.Single(_tags);
        Assert.Contains(_tags[0], x => x.Key == "job_name" && x.Value as string == Ctx.JobName);
    }

    private void ListenLongMetric(string metricName)
    {
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == metricName)
            {
                _longVal++;
                _tags.Add(tags.ToArray());
            }
        });
        _listener.Start();
    }

    private void ListenDoubleMetric(string metricName)
    {
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == metricName)
            {
                _doubleVal = measurement;
                _tags.Add(tags.ToArray());
            }
        });
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
