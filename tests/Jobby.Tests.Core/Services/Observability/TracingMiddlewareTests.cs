using System.Diagnostics;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services.Observability;

public sealed class TracingMiddlewareTests
{
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

    private static ActivityListener CreateListener(Action<Activity> onStopped)
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == JobbyActivitySourceNames.JobsExecution,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = onStopped,
        };
    }

    private static JobExecutionContext CreateCtx()
    {
        return new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };
    }
}
