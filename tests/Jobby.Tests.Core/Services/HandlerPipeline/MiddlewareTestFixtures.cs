using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;

namespace Jobby.Tests.Core.Services.HandlerPipeline;

internal class MwTestCommand : IJobCommand
{
    public List<string> Traces { get; } = new List<string>();

    public static string GetJobName() => "Test";
}

internal class MwTestHandler : IJobCommandHandler<MwTestCommand>
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

internal class FirstMiddleware : IJobbyMiddleware
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

internal class SecondMiddleware : IJobbyMiddleware
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
