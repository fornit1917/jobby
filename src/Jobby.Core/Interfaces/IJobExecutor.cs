using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobExecutor
{
    Task Execute(JobExecutionModel job,
        JobExecutionContext ctx,
        IJobExecutionScope scope,
        IJobParamSerializer serializer);

    JobTypesMetadata GetJobTypesMetadata();
}
