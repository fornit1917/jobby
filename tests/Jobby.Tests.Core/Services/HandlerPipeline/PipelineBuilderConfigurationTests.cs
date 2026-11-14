using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Models;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Services.HandlerPipeline;
using Moq;

namespace Jobby.Tests.Core.Services.HandlerPipeline;

public class PipelineBuilderConfigurationTests
{
    private readonly Mock<IJobExecutionScope> _scopeMock;

    private readonly MwTestCommand _command;
    private readonly MwTestHandler _handler;

    private readonly PipelineBuilderConfiguration _configuration;

    public PipelineBuilderConfigurationTests()
    {
        _scopeMock = new Mock<IJobExecutionScope>();
        _scopeMock
            .Setup(x => x.GetService(It.Is<Type>(t => t == typeof(SecondMiddleware))))
            .Returns(() => new SecondMiddleware());

        _command = new MwTestCommand();
        _handler = new MwTestHandler();

        _configuration = new PipelineBuilderConfiguration();
    }

    private async Task<List<string>> ExecuteViaCreatedBuilder()
    {
        var pipeline = _configuration.CreatePipelineBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        });
        return _command.Traces;
    }

    [Fact]
    public async Task Use_AddsUserMiddlewareToCreatedPipeline()
    {
        _configuration.Use(new FirstMiddleware());

        var traces = await ExecuteViaCreatedBuilder();

        Assert.Equal(["before 1", "inner", "after 1"], traces);
    }

    [Fact]
    public async Task UseByType_AddsUserMiddlewareToCreatedPipeline()
    {
        _configuration.Use<SecondMiddleware>();

        var traces = await ExecuteViaCreatedBuilder();

        Assert.Equal(["before 2", "inner", "after 2"], traces);
    }

    [Fact]
    public async Task UseAsOuter_AddsSystemOuterMiddlewareToCreatedPipeline()
    {
        _configuration.UseAsOuter(new FirstMiddleware());

        var traces = await ExecuteViaCreatedBuilder();

        Assert.Equal(["before 1", "inner", "after 1"], traces);
    }

    [Fact]
    public async Task ClearSystemOuterMiddlewares_RemovesOnlySystemMiddlewares()
    {
        _configuration.Use(new FirstMiddleware());
        _configuration.UseAsOuter(new SecondMiddleware());
        _configuration.ClearSystemOuterMiddlewares();

        var traces = await ExecuteViaCreatedBuilder();

        Assert.Equal(["before 1", "inner", "after 1"], traces);
    }

    [Fact]
    public async Task ClearSystemOuterMiddlewares_EmptyConfiguration_BuildsHandlerWithoutMiddlewares()
    {
        _configuration.ClearSystemOuterMiddlewares();

        var pipeline = _configuration.CreatePipelineBuilder().Build(_handler, _scopeMock.Object);

        Assert.Equal(_handler, pipeline);
        await pipeline.ExecuteAsync(_command, new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        });
        Assert.Equal(["inner"], _command.Traces);
    }

    [Fact]
    public async Task UseAsOuter_CalledMultipleTimes_BuildsPipelineWithAllMiddlewares()
    {
        // Emulates repeated CreateJobbyServer calls: old entries must not be accumulated again
        _configuration.UseAsOuter(new FirstMiddleware());
        _configuration.ClearSystemOuterMiddlewares();
        _configuration.UseAsOuter(new FirstMiddleware());

        var traces = await ExecuteViaCreatedBuilder();

        Assert.Equal(["before 1", "inner", "after 1"], traces);
    }

    [Fact]
    public async Task CreatePipelineBuilder_ReturnsSnapshotIndependentFromLaterConfigChanges()
    {
        _configuration.Use(new FirstMiddleware());
        _configuration.UseAsOuter(new SecondMiddleware());

        var pipeline = _configuration.CreatePipelineBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        });
        Assert.Equal(["before 2", "before 1", "inner", "after 1", "after 2"], _command.Traces);

        // Changes in configuration after builder creation must not affect created builder
        _command.Traces.Clear();
        _configuration.Use<SecondMiddleware>();
        _configuration.UseAsOuter(new FirstMiddleware());
        _configuration.ClearSystemOuterMiddlewares();

        var newCommand = new MwTestCommand();
        await pipeline.ExecuteAsync(newCommand, new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        });
        Assert.Equal(["before 2", "before 1", "inner", "after 1", "after 2"], newCommand.Traces);
    }
}
