using System.Diagnostics.CodeAnalysis;

namespace Jobby.Core.Interfaces.Schedulers;
internal interface ISchedulerExecutorProvider
{
    bool TryGetExecutor(string schedulerType, [NotNullWhen(true)] out ISchedulerExecutor? schedulerExecutor);
}
