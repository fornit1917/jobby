using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Services.HandlerPipeline;

internal class PipelineBuilderConfiguration : IPipelineConfigurable
{
    /// <summary>
    /// Middlewares added by user
    /// </summary>
    private readonly List<MiddlewareDefinition> _userMiddlewares = new();

    /// <summary>
    /// System middlewares (metrics, tracing)
    /// </summary>
    private readonly List<MiddlewareDefinition> _systemOuterMiddlewares = new();
    
    public IPipelineConfigurable Use(IJobbyMiddleware middleware)
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = middleware });
        return this;
    }

    public IPipelineConfigurable Use<TMiddleware>() where TMiddleware : IJobbyMiddleware
    {
        _userMiddlewares.Add(new MiddlewareDefinition { MiddlewareType = typeof(TMiddleware) });
        return this;
    }
    
    /// <summary>
    /// Internal method for add middlewares which should wrap middlewares added by user
    /// </summary>
    /// <param name="middleware"></param>
    internal void UseAsOuter(IJobbyMiddleware middleware)
    {
        _systemOuterMiddlewares.Add(new MiddlewareDefinition { MiddlewareInstance = middleware });
    }

    /// <summary>
    /// Internal method for removing middlewares added by UseAsOuter method
    /// </summary>
    internal void ClearSystemOuterMiddlewares()
    {
        _systemOuterMiddlewares.Clear();
    }

    public IPipelineBuilder CreatePipelineBuilder()
    {
        return new PipelineBuilder(_userMiddlewares.ToArray(), _systemOuterMiddlewares.ToArray());
    }
}