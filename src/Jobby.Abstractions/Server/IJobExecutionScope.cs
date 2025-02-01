namespace Jobby.Abstractions.Server;

public interface IJobExecutionScope : IDisposable
{
    IJobExecutor GetJobExecutor(string jobName);
}
