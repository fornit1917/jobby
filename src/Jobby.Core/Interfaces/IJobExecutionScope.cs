using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

public interface IJobExecutionScope : IDisposable, IAsyncDisposable
{
    object? GetService(Type type);
    
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Default implementation for backward compatibility
        Dispose();
        return ValueTask.CompletedTask;
    }
}
