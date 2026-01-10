using Jobby.Core.Interfaces;
using Jobby.Core.Models;
using Jobby.Samples.AspNet.Db;
using Jobby.Samples.AspNet.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Jobby.Samples.AspNet.Controllers;

[ApiController]
[Route("jobs")]
public class JobsController
{
    private readonly IJobbyClient _jobbyClient;
    private readonly IJobsFactory _jobsFactory;
    private readonly JobbySampleDbContext _dbContext;

    public JobsController(IJobbyClient jobbyClient, IJobsFactory jobsFactory, JobbySampleDbContext dbContext)
    {
        _jobbyClient = jobbyClient;
        _jobsFactory = jobsFactory;
        _dbContext = dbContext;
    }

    [HttpPost("enqueue-job")]
    public async Task<string> EnqueueDemoJob([FromBody] DemoJobCommand command)
    {
        var jobId = await _jobbyClient.EnqueueCommandAsync(command, command.StartAfter ?? DateTime.UtcNow);
        return jobId.ToString();
    }

    [HttpPost("enqueue-job-by-ef")]
    public async Task<string> EnqueueDemoJobByEF([FromBody] DemoJobCommand command)
    {
        var job = _jobsFactory.Create(command);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();
        return job.Id.ToString();
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

    /// <summary>
    /// Enqueues a job as part of a sequence.
    /// Jobs with the same sequenceId will be executed one after another.
    /// </summary>
    [HttpPost("enqueue-job-in-sequence/{sequenceId}")]
    public async Task<string> EnqueueJobInSequence(string sequenceId, [FromBody] DemoJobCommand command)
    {
        var jobId = await _jobbyClient.EnqueueCommandAsync(command, sequenceId);
        return jobId.ToString();
    }

    /// <summary>
    /// Demonstrates a typical workflow: enqueues multiple jobs to a sequence.
    /// Jobs will execute in order - each waits for the previous one to complete.
    /// </summary>
    [HttpPost("enqueue-workflow")]
    public async Task<EnqueueWorkflowResponse> EnqueueWorkflow([FromQuery] int delayMs = 1000)
    {
        // Generate a unique sequence ID for this workflow
        // In real scenarios this could be an order ID, transaction ID, etc.
        var sequenceId = $"workflow-{Guid.NewGuid():N}";

        var jobIds = new List<Guid>();

        // Step 1: Validation job
        var validationJobId = await _jobbyClient.EnqueueCommandAsync(
            new DemoJobCommand { DelayMs = delayMs },
            sequenceId);
        jobIds.Add(validationJobId);

        // Step 2: Processing job
        var processingJobId = await _jobbyClient.EnqueueCommandAsync(
            new DemoJobCommand { DelayMs = delayMs },
            sequenceId);
        jobIds.Add(processingJobId);

        // Step 3: Notification job
        var notificationJobId = await _jobbyClient.EnqueueCommandAsync(
            new DemoJobCommand { DelayMs = delayMs },
            sequenceId);
        jobIds.Add(notificationJobId);

        return new EnqueueWorkflowResponse
        {
            SequenceId = sequenceId,
            JobIds = jobIds
        };
    }
}

public class EnqueueWorkflowResponse
{
    public string SequenceId { get; set; } = string.Empty;
    public List<Guid> JobIds { get; set; } = new();
}
