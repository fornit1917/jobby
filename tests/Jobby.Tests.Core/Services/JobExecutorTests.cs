using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;
public class JobExecutorTests
{
    private const string SerializedCommand = "serialized";

    [Fact]
    public async Task Execute_CallsHandlerWithCorrectParams()
    {
        var command = new TestJobCommand();
        var handler = new TestJobCommandHandler();
        var handlerType = typeof(IJobCommandHandler<TestJobCommand>);

        var scopeFactoryMock = new Mock<IJobExecutionScopeFactory>();
        var serializerMock = new Mock<IJobParamSerializer>();

        serializerMock
            .Setup(x => x.DeserializeJobParam(SerializedCommand, typeof(TestJobCommand)))
            .Returns(() => command);

        var scopeMock = new Mock<IJobExecutionScope>();
        scopeMock.Setup(x => x.GetService(handlerType)).Returns(handler);

        var pipelineBuilderMock = new Mock<IPipelineBuilder>();
        pipelineBuilderMock
            .Setup(x => x.Build(handler, scopeMock.Object))
            .Returns(handler);

        IJobExecutor jobExecutor = new JobExecutor<TestJobCommand, TestJobCommandHandler>();

        var job = new JobExecutionModel
        {
            Id = Guid.NewGuid(),
            JobName = TestJobCommand.GetJobName(),
            JobParam = SerializedCommand,
            StartedCount = 2,
        };
        var ctx = new JobExecutionContext
        {
            CancellationToken = new CancellationTokenSource().Token,
            IsLastAttempt = true,
            IsRecurrent = false,
            JobName = job.JobName,
            StartedCount = job.StartedCount
        };

        var cancelationToken = new CancellationTokenSource().Token;
        await jobExecutor.Execute(job,
            ctx, 
            scopeMock.Object,
            serializerMock.Object,
            pipelineBuilderMock.Object);

        Assert.Equal(command, handler.LatestCommand);
        Assert.Equal(ctx, handler.LatestExecutionContext);
    }

    [Fact]
    public void GetJobTypesMetadata_ReturnsCorrentGetJobTypesMetadata()
    {
        var jobExecutorFactory = new JobExecutor<TestJobCommand, TestJobCommandHandler>();

        var typesMetadata = jobExecutorFactory.GetJobTypesMetadata();

        Assert.Equal(typeof(TestJobCommand), typesMetadata.CommandType);
        Assert.Equal(typeof(IJobCommandHandler<TestJobCommand>), typesMetadata.HandlerType);
        Assert.Equal(typeof(TestJobCommandHandler), typesMetadata.HandlerImplType);
    }
}
