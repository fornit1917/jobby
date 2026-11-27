using System.Diagnostics;
using System.Text.RegularExpressions;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services.Observability;

public sealed class TracingMiddlewareTests
{
    private const string ParentTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string ParentSpanId = "00f067aa0ba902b7";

    private readonly TracingMiddleware _tracingMiddleware = new();
    private readonly Mock<IJobCommandHandler<TestJobCommand>> _handlerMock = new();

    [Fact]
    public async Task ExecuteAsync_HandlerSucceeds_ActivityHasNoErrorStatusAndNoExceptionEvent()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var command = new TestJobCommand();
            var ctx = CreateCtx();

            await _tracingMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Equal(ActivityStatusCode.Unset, captured.Status);
            Assert.DoesNotContain(captured.Events, e => e.Name == "exception");
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_HandlerThrows_SetsErrorStatusAndAddsExceptionEvent()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var command = new TestJobCommand();
            var ctx = CreateCtx();
            var exception = new InvalidOperationException("boom");
            _handlerMock
                .Setup(x => x.ExecuteAsync(command, ctx))
                .ThrowsAsync(exception);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _tracingMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object));

            Assert.NotNull(captured);
            Assert.Equal(ActivityStatusCode.Error, captured.Status);
            Assert.Equal("boom", captured.StatusDescription);

            var exceptionEvent = Assert.Single(captured.Events, e => e.Name == "exception");
            string? GetTag(string key) => exceptionEvent.Tags.First(t => t.Key == key).Value as string;
            Assert.Equal(exception.GetType().FullName, GetTag("exception.type"));
            Assert.Equal(exception.Message, GetTag("exception.message"));
            Assert.Contains(exception.Message, GetTag("exception.stacktrace"));
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RestoreMode_ValidTraceparent_ActivityContinuesTraceWithCapturedParentSpan()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var traceparent = $"00-{ParentTraceId}-{ParentSpanId}-01";
            var ctx = CreateCtx(traceparent, JobbyTracingMode.RestoreTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Equal(ParentTraceId, captured.TraceId.ToHexString());
            // the real parent span id is restored, so the job span becomes a child of the captured span
            Assert.Equal(ParentSpanId, captured.ParentSpanId.ToHexString());
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RestoreMode_SampledTraceparent_SamplerSeesFullParentContextWithRecordedFlags()
    {
        var observed = new List<ActivityContext>();
        var listener = CreateParentBasedListener(observed);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var traceparent = $"00-{ParentTraceId}-{ParentSpanId}-01";
            var ctx = CreateCtx(traceparent, JobbyTracingMode.RestoreTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            // the sampler must receive the full parsed parent context, including the "01" flags
            var parent = Assert.Single(observed);
            Assert.Equal(ParentTraceId, parent.TraceId.ToHexString());
            Assert.Equal(ParentSpanId, parent.SpanId.ToHexString());
            Assert.Equal(ActivityTraceFlags.Recorded, parent.TraceFlags);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RestoreMode_UnsampledTraceparent_SamplerSeesUnsampledFlagsAndActivityIsNotRecorded()
    {
        Activity? captured = null;
        var observed = new List<ActivityContext>();
        var listener = CreateParentBasedListener(observed, a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var traceparent = $"00-{ParentTraceId}-{ParentSpanId}-00";
            var ctx = CreateCtx(traceparent, JobbyTracingMode.RestoreTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            // the original unsampled trace is propagated honestly: the sampler sees "00" flags
            var parent = Assert.Single(observed);
            Assert.Equal(ParentTraceId, parent.TraceId.ToHexString());
            Assert.Equal(ActivityTraceFlags.None, parent.TraceFlags);

            // and the job activity itself stays unrecorded
            Assert.NotNull(captured);
            Assert.Equal(ParentTraceId, captured.TraceId.ToHexString());
            Assert.Equal(ActivityTraceFlags.None, captured.ActivityTraceFlags);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_LinkMode_ValidTraceparent_StartsNewTraceWithLink()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var traceparent = $"00-{ParentTraceId}-{ParentSpanId}-01";
            var ctx = CreateCtx(traceparent, JobbyTracingMode.LinkTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            // a new root trace is started
            Assert.NotEqual(ParentTraceId, captured.TraceId.ToHexString());
            Assert.Equal(default, captured.ParentSpanId);

            var link = Assert.Single(captured.Links);
            Assert.Equal(ParentTraceId, link.Context.TraceId.ToHexString());
            Assert.Equal(ParentSpanId, link.Context.SpanId.ToHexString());
            Assert.Equal(ActivityTraceFlags.Recorded, link.Context.TraceFlags);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_CreateNewMode_ValidTraceparent_StartsNewTraceWithoutLink()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var traceparent = $"00-{ParentTraceId}-{ParentSpanId}-01";
            var ctx = CreateCtx(traceparent, JobbyTracingMode.CreateNewTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.NotEqual(ParentTraceId, captured.TraceId.ToHexString());
            Assert.Equal(default, captured.ParentSpanId);
            Assert.Empty(captured.Links);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RestoreMode_InvalidTraceparent_StartsNewTrace()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var ctx = CreateCtx("not-a-traceparent", JobbyTracingMode.RestoreTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Matches(new Regex("^[0-9a-f]{32}$"), captured.TraceId.ToHexString());
            Assert.Equal(default, captured.ParentSpanId);
            Assert.Empty(captured.Links);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_LinkMode_InvalidTraceparent_StartsNewTraceWithoutLink()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var ctx = CreateCtx("00-bad", JobbyTracingMode.LinkTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Equal(default, captured.ParentSpanId);
            Assert.Empty(captured.Links);
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_RestoreMode_TraceparentIsNull_StartsNewTrace()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var ctx = CreateCtx(traceparent: null, JobbyTracingMode.RestoreTraceId);

            await _tracingMiddleware.ExecuteAsync(new TestJobCommand(), ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Equal(default, captured.ParentSpanId);
            Assert.Empty(captured.Links);
        }
        finally
        {
            listener.Dispose();
        }
    }

    /// <summary>
    /// Listener emulating the standard OTel ParentBased sampler: it samples only when
    /// the parent context flags say Recorded. Every parent context the middleware passes
    /// to StartActivity is recorded into <paramref name="observedParents"/>, so tests can
    /// verify the exact context (trace id, span id, flags) the sampler received.
    /// </summary>
    private static ActivityListener CreateParentBasedListener(List<ActivityContext> observedParents,
        Action<Activity>? onStopped = null)
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == JobbyActivitySourceNames.JobsExecution,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                observedParents.Add(options.Parent);
                return (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.PropagationData;
            },
            ActivityStopped = onStopped ?? (_ => { }),
        };
    }

    [Fact]
    public async Task ExecuteAsync_ActivityIsNamedAfterTheJob_NoJobNameTag()
    {
        Activity? captured = null;
        var listener = CreateListener(a => captured = a);
        ActivitySource.AddActivityListener(listener);
        try
        {
            var command = new TestJobCommand();
            var ctx = CreateCtx();

            await _tracingMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);

            Assert.NotNull(captured);
            Assert.Equal(TestJobCommand.GetJobName(), captured.DisplayName);
            // the job_name tag is gone — the job name is carried by the activity name itself
            Assert.DoesNotContain(captured.Tags, t => t.Key == "job_name");
        }
        finally
        {
            listener.Dispose();
        }
    }

    private static ActivityListener CreateListener(Action<Activity> onStopped,
        ActivitySamplingResult samplingResult = ActivitySamplingResult.AllData)
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == JobbyActivitySourceNames.JobsExecution,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => samplingResult,
            ActivityStopped = onStopped,
        };
    }

    private static JobExecutionContext CreateCtx(string? traceparent = null,
        JobbyTracingMode tracingMode = JobbyTracingMode.CreateNewTraceId)
    {
        return new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
            Traceparent = traceparent,
            TracingMode = tracingMode,
        };
    }
}
