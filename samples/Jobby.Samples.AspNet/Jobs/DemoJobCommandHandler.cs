using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Samples.AspNet.Exceptions;

namespace Jobby.Samples.AspNet.Jobs;

public class DemoJobCommandHandler : IJobCommandHandler<DemoJobCommand>
{
    private readonly ILogger<DemoJobCommandHandler> _logger;

    public DemoJobCommandHandler(ILogger<DemoJobCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(DemoJobCommand command, JobExecutionContext ctx)
    {
        _logger.LogInformation("Job {JobName} started, attempt number is {Attempt}, {Time}", 
            DemoJobCommand.GetJobName(), ctx.StartedCount, DateTime.UtcNow);
        
        if (command.ShouldBeFailed)
            throw new Exception("Job is configured to fail");

        if (command.ShouldThrowIgnoredException)
            throw new ExceptionShouldBeIgnored("Job is configure to throw ignored exception");

        await Task.Delay(command.DelayMs);

        _logger.LogInformation("Job {JobName} completed, {Time}", DemoJobCommand.GetJobName(), DateTime.UtcNow);
    }
}
