namespace Jobby.Core.Interfaces;

public interface IJobExecutionScopeFactory
{
    IJobExecutionScope CreateJobExecutionScope();
}
