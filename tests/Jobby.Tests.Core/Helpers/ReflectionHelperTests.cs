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
    public void TryGetJobNameByType_ExplicitInterfaceImplementation_ReturnsJobName()
    {
        var jobName = ReflectionHelper.TryGetJobNameByType(typeof(ExplicitImplJobCommand));
        Assert.Equal("ExplicitImplJob", jobName);
    }

    [Fact]
    public void TryGetJobNameByType_MultipleExplicitImplementations_ReturnsJobCommandImplementation()
    {
        var jobName = ReflectionHelper.TryGetJobNameByType(typeof(MultipleExplicitImplsCommand));
        Assert.Equal("MultipleExplicitImplsJob", jobName);
    }

    [Fact]
    public void TryGetJobNameByType_NotJobCommand_ReturnsNull()
    {
        var jobName = ReflectionHelper.TryGetJobNameByType(typeof(string));
        Assert.Null(jobName);
    }

    [Fact]
    public void TryGetCommandTypesFromHandlerType_JobHandler_ReturnsCommandType()
    {
        var commandTypes = ReflectionHelper.TryGetCommandTypesFromHandlerType(typeof(TestJobHandler));
        Assert.Equal([typeof(TestJobCommand)], commandTypes);
    }

    [Fact]
    public void TryGetCommandTypesFromHandlerType_NotJobHandler_ReturnsEmpty()
    {
        var commandTypes = ReflectionHelper.TryGetCommandTypesFromHandlerType(typeof(string));
        Assert.Empty(commandTypes);
    }

    [Fact]
    public void TryGetCommandTypesFromHandlerType_MultiCommandHandler_ReturnsAllCommandTypes()
    {
        var commandTypes = ReflectionHelper.TryGetCommandTypesFromHandlerType(typeof(MultiCommandJobHandler));
        Assert.Equal(
            new HashSet<Type> { typeof(FirstTestJobCommand), typeof(SecondTestJobCommand) },
            commandTypes.ToHashSet());
    }

    [Fact]
    public void TryGetCommandTypesFromHandlerType_DerivedHandler_ReturnsCommandTypeFromBaseInterfaces()
    {
        var commandTypes = ReflectionHelper.TryGetCommandTypesFromHandlerType(typeof(DerivedJobHandler));
        Assert.Equal([typeof(TestJobCommand)], commandTypes);
    }

    private class TestJobCommand : IJobCommand
    {
        public static string GetJobName() => "TestJob";
    }

    private class ExplicitImplJobCommand : IJobCommand
    {
        static string IJobCommand.GetJobName() => "ExplicitImplJob";
    }

    private interface IOtherNamedCommand
    {
        static abstract string GetJobName();
    }

    private class MultipleExplicitImplsCommand : IJobCommand, IOtherNamedCommand
    {
        static string IJobCommand.GetJobName() => "MultipleExplicitImplsJob";
        static string IOtherNamedCommand.GetJobName() => "OtherJob";
    }

    private class FirstTestJobCommand : IJobCommand
    {
        public static string GetJobName() => "FirstTestJob";
    }

    private class SecondTestJobCommand : IJobCommand
    {
        public static string GetJobName() => "SecondTestJob";
    }

    private class TestJobHandler : IJobCommandHandler<TestJobCommand>
    {
        public Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
        {
            return Task.CompletedTask;
        }
    }

    private class MultiCommandJobHandler : IJobCommandHandler<FirstTestJobCommand>, IJobCommandHandler<SecondTestJobCommand>
    {
        public Task ExecuteAsync(FirstTestJobCommand command, JobExecutionContext ctx)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(SecondTestJobCommand command, JobExecutionContext ctx)
        {
            return Task.CompletedTask;
        }
    }

    private abstract class BaseJobHandler : IJobCommandHandler<TestJobCommand>
    {
        public abstract Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx);
    }

    private class DerivedJobHandler : BaseJobHandler
    {
        public override Task ExecuteAsync(TestJobCommand command, JobExecutionContext ctx)
        {
            return Task.CompletedTask;
        }
    }
}
