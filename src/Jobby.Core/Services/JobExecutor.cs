using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;
internal readonly struct JobExecutor<TCommand> : IJobExecutor
    where TCommand : IJobCommand
{
    public readonly TCommand Command;
    public readonly IJobCommandHandler<TCommand> Handler;

    public JobExecutor(TCommand command, IJobCommandHandler<TCommand> handler)
    {
        Command = command;
        Handler = handler;
    }

    public Task ExecuteJob(JobExecutionContext ctx)
        => Handler.ExecuteAsync(Command, ctx);
}
