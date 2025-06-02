using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Samples.AspNetSimple.Jobs;

public class DemoJobCommandHandler : IJobCommandHandler<DemoJobCommand>
{
    private readonly ILogger<DemoJobCommandHandler> _logger;

    public DemoJobCommandHandler(ILogger<DemoJobCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(DemoJobCommand command, JobExecutionContext ctx)
    {
        _logger.LogInformation("Job {JobName} started, attempt number is {Attempt}", 
            DemoJobCommand.GetJobName(), ctx.StartedCount);
        
        if (command.ShouldBeFailed)
            throw new Exception("Job is configured to fail");

        await Task.Delay(command.DelayMs);

        _logger.LogInformation("Job {JobName} completed", DemoJobCommand.GetJobName());
    }
}
