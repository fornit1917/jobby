using Jobby.Core.Models;

namespace Jobby.Core.Interfaces.Observability;

internal interface IMetricsService
{
    void AddStarted(JobExecutionContext ctx);
    void AddCompleted(JobExecutionContext ctx);
    void AddRetried(JobExecutionContext ctx);
    void AddFailed(JobExecutionContext ctx);
    void AddDuration(JobExecutionContext ctx, double duration);
}
