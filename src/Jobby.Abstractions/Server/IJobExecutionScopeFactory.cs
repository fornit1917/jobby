namespace Jobby.Abstractions.Server;

public interface IJobExecutionScopeFactory
{
    IJobExecutionScope CreateJobExecutionScope();
}
