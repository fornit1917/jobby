using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobExecutionScope : IDisposable
{
    object? GetService(Type type);
}
