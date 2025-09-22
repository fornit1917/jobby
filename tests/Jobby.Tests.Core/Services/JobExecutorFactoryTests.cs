using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Core.Services;
using Jobby.TestsUtils.Jobs;
using Moq;

namespace Jobby.Tests.Core.Services;
public class JobExecutorFactoryTests
{
    private const string SerializedCommand = "serialized";

    [Fact]
    public void CreateJobExecutor_CreateAppropriateConcreteExecutor()
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

        IJobExecutorFactory jobExecutorFactory = new JobExecutorFactory<TestJobCommand, TestJobCommandHandler>();

        var jobExecutor = jobExecutorFactory.CreateJobExecutor(scopeMock.Object, serializerMock.Object, SerializedCommand);
        var jobTypesMetadata = jobExecutorFactory.GetJobTypesMetadata();

        if (jobExecutor is not JobExecutor<TestJobCommand> { } concreteJobExecutor)
            Assert.Fail($"{nameof(jobExecutor)} expected to be of type {typeof(JobExecutor<TestJobCommand>)} but {jobExecutor.GetType()} is actual");
        else
            Assert.Equal(
                expected: new JobExecutor<TestJobCommand>(command, handler),
                actual: jobExecutor
            );
    }
}
