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

    private readonly PipelineBuilder _pipelineBuilder;

    public PipelineBuilderTests()
    {
        _scopeMock = new Mock<IJobExecutionScope>();
        _scopeMock
            .Setup(x => x.GetService(It.Is<Type>(t => t == typeof(SecondMiddleware))))
            .Returns(() => new SecondMiddleware());

        _command = new MwTestCommand();
        _handler = new MwTestHandler();
           
        _pipelineBuilder = new PipelineBuilder();
    }

    [Fact]
    public void NoMiddlewares_ReturnsHandler()
    {
        var pipeline = _pipelineBuilder.Build(_handler, _scopeMock.Object);
        Assert.Equal(_handler, pipeline);
    }

    [Fact]
    public async Task OneUserMiddleware_BuildsCorrectPipeline()
    {
        _pipelineBuilder
            .Use(new FirstMiddleware());

        var pipeline = _pipelineBuilder.Build(_handler, _scopeMock.Object);

        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        };
        await pipeline.ExecuteAsync(_command, ctx);

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(ctx, _handler.PassedContext);
        Assert.Equal(["before 1", "inner", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task OneSystemOuterMiddleware_BuildsCorrectPipeline()
    {
        _pipelineBuilder
            .UseAsOuter(new FirstMiddleware());

        var pipeline = _pipelineBuilder.Build(_handler, _scopeMock.Object);

        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        };
        await pipeline.ExecuteAsync(_command, ctx);

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(ctx, _handler.PassedContext);
        Assert.Equal(["before 1", "inner", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task TwoUserMiddlewares_BuildsCorrectPipeline()
    {
        _pipelineBuilder
            .Use(new FirstMiddleware())
            .Use<SecondMiddleware>();

        var pipeline = _pipelineBuilder.Build(_handler, _scopeMock.Object);

        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        };
        await pipeline.ExecuteAsync(_command, ctx);

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(ctx, _handler.PassedContext);
        Assert.Equal(["before 1", "before 2", "inner", "after 2", "after 1"], _command.Traces);
    }

    [Fact]
    public async Task UserAndOuterSystemMiddlewares_BuildsCorrectPipeline()
    {
        _pipelineBuilder.Use<SecondMiddleware>();
        _pipelineBuilder.UseAsOuter(new FirstMiddleware());
            

        var pipeline = _pipelineBuilder.Build(_handler, _scopeMock.Object);

        var ctx = new JobExecutionContext
        {
            CancellationToken = CancellationToken.None,
            IsLastAttempt = false,
            IsRecurrent = false,
            JobName = MwTestCommand.GetJobName(),
            StartedCount = 123,
        };
        await pipeline.ExecuteAsync(_command, ctx);

        Assert.Equal(_command, _handler.PassedCommand);
        Assert.Equal(ctx, _handler.PassedContext);
        Assert.Equal(["before 1", "before 2", "inner", "after 2", "after 1"], _command.Traces);
    }

    private class MwTestCommand : IJobCommand
    {
        public List<string> Traces { get; } = new List<string>();

        public static string GetJobName() => "Test";
        public bool CanBeRestarted() => true;
    }

    private class MwTestHandler : IJobCommandHandler<MwTestCommand>
    {
        public MwTestCommand? PassedCommand { get; private set; }
        public JobExecutionContext PassedContext { get; private set; }

        public Task ExecuteAsync(MwTestCommand command, JobExecutionContext ctx)
        {
            command.Traces.Add("inner");
            PassedCommand = command;
            PassedContext = ctx;
            return Task.CompletedTask;
        }
    }

    private class FirstMiddleware : IJobbyMiddleware
    {
        public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler) 
            where TCommand : IJobCommand
        {
            var testCommand = command as MwTestCommand;
            if (testCommand != null)
            {
                testCommand.Traces.Add("before 1");
            }

            await handler.ExecuteAsync(command, ctx);

            if (testCommand != null)
            {
                testCommand.Traces.Add("after 1");
            }
        }
    }

    private class SecondMiddleware : IJobbyMiddleware
    {
        public async Task ExecuteAsync<TCommand>(TCommand command, JobExecutionContext ctx, IJobCommandHandler<TCommand> handler)
            where TCommand : IJobCommand
        {
            var testCommand = command as MwTestCommand;
            if (testCommand != null)
            {
                testCommand.Traces.Add("before 2");
            }

            await handler.ExecuteAsync(command, ctx);

            if (testCommand != null)
            {
                testCommand.Traces.Add("after 2");
            }
        }
    }
}
