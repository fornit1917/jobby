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
        var opts = new JobOpts
        {
            StartTime = command.StartAfter ?? DateTime.UtcNow,
            SerializableGroupId = command.SerializableGroupId,
            LockGroupIfFailed = command.LockGroupIfFailed,
        };
        var jobId = await _jobbyClient.EnqueueCommandAsync(command, opts);
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

    [HttpPost("enqueue-batch")]
    public async Task<List<string>> EnqueueBatch([FromBody] List<DemoJobCommand> commands)
    {
        var jobs = new List<JobCreationModel>(commands.Count);
        foreach (var command in commands)
        {
            var job = _jobsFactory.Create(command);
            jobs.Add(job);
        }
        await _jobbyClient.EnqueueBatchAsync(jobs);
        return jobs.Select(x => x.Id.ToString()).ToList();
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
}
