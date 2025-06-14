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
}
