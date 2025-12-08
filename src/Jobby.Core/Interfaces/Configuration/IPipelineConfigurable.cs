using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Interfaces.Configuration;

public interface IPipelineConfigurable
{
    IPipelineConfigurable Use(IJobbyMiddleware middleware);
    IPipelineConfigurable Use<TMiddleware>() where TMiddleware : IJobbyMiddleware;
}
