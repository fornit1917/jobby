using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;
internal interface IJobExecutor
{
    Task ExecuteJob(JobExecutionContext ctx);
}
