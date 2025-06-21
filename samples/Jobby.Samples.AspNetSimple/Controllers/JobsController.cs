using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Samples.AspNetSimple.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Jobby.Samples.AspNetSimple.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController
{
    private readonly IJobbyClient _jobbyClient;
    private readonly IJobsFactory _jobsFactory;

    public JobsController(IJobbyClient jobbyClient, IJobsFactory jobsFactory)
    {
        _jobbyClient = jobbyClient;
        _jobsFactory = jobsFactory;
    }

    [HttpPost("run-job")]
    public async Task<string> RunDemoJob([FromBody] DemoJobCommand command)
    {
        var jobId = await _jobbyClient.EnqueueCommandAsync(command, command.StartAfter ?? DateTime.UtcNow);
        return jobId.ToString();
    }

    [HttpPost("cancel-job/{jobId}")]
    public async Task<string> CancelJob(Guid jobId)
    {
        await _jobbyClient.CancelJobsByIdsAsync(jobId);
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

    [HttpGet("show-job-model")]
    public JobCreationModel ShowJobModel(DateTime? startTime = null)
    {
        startTime ??= DateTime.UtcNow;
        var command = new DemoJobCommand();
        return _jobsFactory.Create(command, startTime.Value);
    }
}
