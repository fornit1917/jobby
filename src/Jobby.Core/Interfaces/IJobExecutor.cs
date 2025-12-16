using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;

internal interface IJobExecutor
{
    Task Execute(JobExecutionModel job,
        JobExecutionContext ctx,
        IJobExecutionScope scope,
        IJobParamSerializer serializer,
        IPipelineBuilder pipelineBuilder);

    JobTypesMetadata GetJobTypesMetadata();
}
