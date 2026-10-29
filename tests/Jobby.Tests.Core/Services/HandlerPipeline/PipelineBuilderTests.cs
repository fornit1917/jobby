using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Jobby.Core.Services.HandlerPipeline;
using Moq;

namespace Jobby.Tests.Core.Services.HandlerPipeline;

public class PipelineBuilderTests
{
    private readonly Mock<IJobExecutionScope> _scopeMock;

    private readonly MwTestCommand _command;
    private readonly MwTestHandler _handler;

    private readonly List<MiddlewareDefinition> _userMiddlewares = new();
    private readonly List<MiddlewareDefinition> _systemOuterMiddlewares = new();

    public PipelineBuilderTests()
    {
        _scopeMock = new Mock<IJobExecutionScope>();
        _scopeMock
            .Setup(x => x.GetService(It.Is<Type>(t => t == typeof(SecondMiddleware))))
            .Returns(() => new SecondMiddleware());

        _command = new MwTestCommand();
        _handler = new MwTestHandler();
    }

    private PipelineBuilder CreateBuilder()
    {
        return new PipelineBuilder(_userMiddlewares, _systemOuterMiddlewares);
    }

    private JobExecutionContext CreateContext()
    {
        return new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        };
    }

    [Fact]
    public void NoMiddlewares_ReturnsHandler()
    {
        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        Assert.Equal(_handler, pipeline);
    }

    [Fact]
    public async Task OneUserMiddlewareInstance_BuildsCorrectPipeline()
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = new FirstMiddleware() });

        var ctx = CreateContext();
        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, ctx);

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(ctx, _handler.PassedContext);
        Assert.Equal(["before 1", "inner", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task OneUserMiddlewareByType_BuildsCorrectPipeline()
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareType = typeof(SecondMiddleware) });

        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, CreateContext());

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(["before 2", "inner", "after 2"], _command.Traces);
    }

    [Fact]
    public async Task OneSystemOuterMiddleware_BuildsCorrectPipeline()
    {
        _systemOuterMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = new FirstMiddleware() });

        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, CreateContext());

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(["before 1", "inner", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task TwoUserMiddlewares_BuildsCorrectPipeline()
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = new FirstMiddleware() });
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareType = typeof(SecondMiddleware) });

        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, CreateContext());

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(["before 1", "before 2", "inner", "after 2", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task UserAndSystemOuterMiddlewares_BuildsCorrectPipeline()
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareType = typeof(SecondMiddleware) });
        _systemOuterMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = new FirstMiddleware() });

        var pipeline = CreateBuilder().Build(_handler, _scopeMock.Object);
        await pipeline.ExecuteAsync(_command, CreateContext());

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(["before 1", "before 2", "inner", "after 2", "after 1"], _command.Traces);
    }

    [Fact]
    public void MiddlewareWithoutInstanceAndType_ThrowsInvalidMiddlewaresConfigException()
    {
        _userMiddlewares.Add(new MiddlewareDefinition());
        
        Assert.Throws<InvalidMiddlewaresConfigException>(
            () => CreateBuilder().Build(_handler, _scopeMock.Object));
    }
}
