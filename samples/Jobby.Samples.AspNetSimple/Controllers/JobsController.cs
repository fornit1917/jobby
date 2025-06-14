using Jobby.Core.Interfaces;
using Jobby.Samples.AspNetSimple.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Jobby.Samples.AspNetSimple.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController
{
    private readonly IJobbyClient _jobbyClient;

    public JobsController(IJobbyClient jobbyClient)
    {
        _jobbyClient = jobbyClient;
    }

    [HttpPost("demo-job")]
    public async Task<string> RunDemoJob([FromBody] DemoJobCommand command)
    {
        await _jobbyClient.EnqueueCommandAsync(command);
        return "ok";
    }

    [HttpPost("schedule-recurrent")]
    public async Task<string> ScheduleRecurrent([FromBody] string cron = "*/5 * * * * *")
    {
        var command = new EmptyRecurrentJobCommand();
        await _jobbyClient.ScheduleRecurrentAsync(command, cron);
        return "ok";
    }

    [HttpPost("cancel-recurrent")]
    public async Task<string> CancelRecurrent()
    {
        await _jobbyClient.CancelRecurrentAsync<EmptyRecurrentJobCommand>();
        return "ok";
    }
}
