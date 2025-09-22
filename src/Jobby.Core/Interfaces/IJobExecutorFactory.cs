using Jobby.Core.Models;

namespace Jobby.Core.Interfaces;
internal interface IJobExecutorFactory
{
    IJobExecutor CreateJobExecutor(IJobExecutionScope scope, IJobParamSerializer serializer, string? jobParam);
    JobTypesMetadata GetJobTypesMetadata();
}
