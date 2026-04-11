using Jobby.Core.Interfaces;
using Jobby.Core.Models;

namespace Jobby.TestsUtils.Jobs;

public class TestJobWithDefaultOptsCommand : IJobCommand, IHasDefaultJobOptions
{
    private readonly JobOpts _defaultJobOpts;
    private readonly RecurrentJobOpts _defaultRecurrentJobOpts;

    public TestJobWithDefaultOptsCommand(JobOpts defaultJobOpts, RecurrentJobOpts defaultRecurrentJobOpts)
    {
        _defaultJobOpts = defaultJobOpts;
        _defaultRecurrentJobOpts = defaultRecurrentJobOpts;
    }

    public static string GetJobName() => "TestJobWithDefaultOpts";

    public JobOpts GetOptionsForEnqueuedJob() => _defaultJobOpts;
    public RecurrentJobOpts GetOptionsForRecurrentJob() => _defaultRecurrentJobOpts;
}