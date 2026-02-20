using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.AspNet.Jobs;

public class CustomSchedulerRecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => nameof(CustomSchedulerRecurrentJobCommand);
}

public class CustomSchedulerRecurrentJobCommandHandler : IJobCommandHandler<CustomSchedulerRecurrentJobCommand>
{
    private readonly ILogger<CustomSchedulerRecurrentJobCommandHandler> _logger;

    public CustomSchedulerRecurrentJobCommandHandler(ILogger<CustomSchedulerRecurrentJobCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CustomSchedulerRecurrentJobCommand command, JobExecutionContext ctx)
    {
        _logger.LogInformation("CustomSchedulerRecurrentJob executed, {Time}", DateTime.UtcNow);
        return Task.CompletedTask;
    }
}