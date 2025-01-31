using Jobby.Abstractions.Models;

namespace Jobby.Abstractions.Client;

public interface IJobsMediator
{
    Task<long> EnqueueAsync<T>(T command) where T : IJobCommand;
}
