using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Observability;
using Jobby.Core.Models;
using Jobby.Core.Services.Observability;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services.Observability;

public sealed class MetricsMiddlewareTests 
{
    private readonly Mock<IMetricsService> _metricsMock;

    private readonly Mock<IJobCommandHandler<TestJobCommand>> _handlerMock;

    private readonly MetricsMiddleware _metricsMiddleware;

    public MetricsMiddlewareTests()
    {
        _handlerMock = new Mock<IJobCommandHandler<TestJobCommand>>();
        _metricsMock = new Mock<IMetricsService>();
        _metricsMiddleware = new MetricsMiddleware(_metricsMock.Object);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Any_IncrementsStartedCount(bool isError, bool isRecurrent, bool isLastAttempt)
    {
        var command = new TestJobCommand();
        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = isLastAttempt,
            IsRecurrent = isRecurrent,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };
        if (isError)
        {
            _handlerMock
                .Setup(x => x.ExecuteAsync(command, ctx))
                .ThrowsAsync(new Exception("err"));
        }

        try
        {
            await _metricsMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);
        }
        catch (Exception)
        {
        }

        _metricsMock.Verify(x => x.AddStarted(ctx), Times.Once);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Any_WritesDuration(bool isError, bool isRecurrent, bool isLastAttempt)
    {
        var command = new TestJobCommand();
        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = isLastAttempt,
            IsRecurrent = isRecurrent,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };
        if (isError)
        {
            _handlerMock
                .Setup(x => x.ExecuteAsync(command, ctx))
                .ThrowsAsync(new Exception("err"));
        }

        try
        {
            await _metricsMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);
        }
        catch (Exception)
        {
        }

        _metricsMock.Verify(x => x.AddDuration(ctx, It.Is<double>(d => d > 0)), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Success_IncrementsCompletedCount(bool isRecurrent)
    {
        var command = new TestJobCommand();
        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = isRecurrent,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };

        await _metricsMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);

        _metricsMock.Verify(x => x.AddCompleted(ctx));
        _metricsMock.Verify(x => x.AddFailed(ctx), Times.Never);
        _metricsMock.Verify(x => x.AddRetried(ctx), Times.Never);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Error_LastAttemptOrRecurrent_IncrementsFailedCount(bool isRecurrent, bool isLastAttempt)
    {
        var command = new TestJobCommand();
        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = isLastAttempt,
            IsRecurrent = isRecurrent,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };
        _handlerMock
            .Setup(x => x.ExecuteAsync(command, ctx))
            .ThrowsAsync(new Exception("err"));

        try
        {
            await _metricsMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);
        }
        catch (Exception)
        {
        }

        _metricsMock.Verify(x => x.AddFailed(ctx), Times.Once);
        _metricsMock.Verify(x => x.AddRetried(ctx), Times.Never);
        _metricsMock.Verify(x => x.AddCompleted(ctx), Times.Never);
    }

    [Fact]
    public async Task Error_NotRecurrent_NotLastAttempt_IncrementsRetriedCount()
    {
        var command = new TestJobCommand();
        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = TestJobCommand.GetJobName(),
            StartedCount = 1,
        };
        _handlerMock
            .Setup(x => x.ExecuteAsync(command, ctx))
            .ThrowsAsync(new Exception("err"));

        try
        {
            await _metricsMiddleware.ExecuteAsync(command, ctx, _handlerMock.Object);
        }
        catch (Exception)
        {
        }

        _metricsMock.Verify(x => x.AddRetried(ctx), Times.Once);
        _metricsMock.Verify(x => x.AddFailed(ctx), Times.Never);
        _metricsMock.Verify(x => x.AddCompleted(ctx), Times.Never);
    }
}
