using Jobby.Core.Helpers;
using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Tests.Core.Helpers;

public class ReflectionHelperTests
{
    [Fact]
    public void TryGetJobNameByType_JobCommand_ReturnsJobName()
    {
        var jobName = ReflectionHelper.TryGetJobNameByType(typeof(TestJobCommand));
        Assert.Equal(TestJobCommand.GetJobName(), jobName);
    }

    [Fact]
    public void TryGetJobNameByType_NotJobCommand_ReturnsNull()
    {
        var jobName = ReflectionHelper.TryGetJobNameByType(typeof(string));
        Assert.Null(jobName);
    }

    [Fact]
    public void TryGetCommandTypeFromHandlerType_JobHandler_ReturnsCommandType()
    {
        var commandType = ReflectionHelper.TryGetCommandTypeFromHandlerType(typeof(TestJobHandler));
        Assert.Equal(typeof(TestJobCommand), commandType);
    }

    [Fact]
    public void TryGetCommandTypeFromHandlerType_NotJobHandler_ReturnNull()
    {
        var commandType = ReflectionHelper.TryGetCommandTypeFromHandlerType(typeof(string));
        Assert.Null(commandType);
    }

    private class TestJobCommand : IJobCommand
    {
        public static string GetJobName() => "TestJob";
        public bool CanBeRestarted() => false;
    }

    private class TestJobHandler : IJobCommandHandler<TestJobCommand>
    {
        public Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
        {
            return Task.CompletedTask;
        }
    }
}
