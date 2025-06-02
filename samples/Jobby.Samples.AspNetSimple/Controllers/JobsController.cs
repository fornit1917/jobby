using Jobby.Core.Interfaces;
using Jobby.Samples.AspNetSimple.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Jobby.Samples.AspNetSimple.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController
{
    private readonly IJobsClient _jobbyClient;

    public JobsController(IJobsClient jobbyClient)
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
