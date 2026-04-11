using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IHasDefaultJobOptions
{
    public JobOpts GetOptionsForEnqueuedJob();
    public RecurrentJobOpts GetOptionsForRecurrentJob();
}