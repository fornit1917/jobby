using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.Core.Services;

public class JobsSequenceBuilder
{
    private readonly IJobsFactory _factory;
    private readonly List<JobCreationModel> _jobs;

    public JobsSequenceBuilder(IJobsFactory factory)
    {
        _jobs = new List<JobCreationModel>();
        _factory = factory;
    }

    public JobsSequenceBuilder(int capacity, IJobsFactory factory)
    {
        _jobs = new List<JobCreationModel>(capacity);
        _factory = factory;
    }

    public JobsSequenceBuilder Add<TCommand>(TCommand command) where TCommand : IJobCommand
    {
        return Add(command, DateTime.UtcNow);
    }

    public JobsSequenceBuilder Add<TCommand>(TCommand command, DateTime startTime) where TCommand: IJobCommand
    {
        var job = _factory.Create(command, startTime);
        _jobs.Add(job);
        if (_jobs.Count > 1)
        {
            job.Status = JobStatus.WaitingPrev;
            _jobs[_jobs.Count - 2].NextJobId = job.Id;
        }
        return this;
    }

    public List<JobCreationModel> GetJobs()
    {
        return _jobs;
    }
}
