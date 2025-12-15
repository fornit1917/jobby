using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Services.HandlerPipeline;

internal record struct MiddlewareDefinition
{
    public IJobbyMiddleware? MiddlewareInstance { get; init; }
    public Type? MiddlewareType { get; init; }
}
