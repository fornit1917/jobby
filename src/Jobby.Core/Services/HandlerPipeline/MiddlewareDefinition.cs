using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Services.HandlerPipeline;

internal class MiddlewareDefinition
{
    public IJobbyMiddleware? MiddlewareInstance { get; init; }
    public Type? MiddlewareType { get; init; }
}
