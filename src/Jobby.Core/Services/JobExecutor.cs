using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;
internal readonly struct JobExecutor<TCommand> : IJobExecutor
    where TCommand : IJobCommand
{
    private readonly TCommand _command;
    private readonly IJobCommandHandler<TCommand> _handler;

    public JobExecutor(TCommand command, IJobCommandHandler<TCommand> handler)
    {
        _command = command;
        _handler = handler;
    }

    public Task ExecuteJob(JobExecutionContext ctx)
        => _handler.ExecuteAsync(_command, ctx);
}
