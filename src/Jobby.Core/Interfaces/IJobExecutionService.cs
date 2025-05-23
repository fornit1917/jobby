using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobExecutionService : IDisposable
{
    Task ExecuteCommand(Job job, CancellationToken cancellationToken);
    Task ExecuteRecurrent(Job job, CancellationToken cancellationToken);
}
