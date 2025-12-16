using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Services.HandlerPipeline;

internal class PipelineBuilder : IPipelineConfigurable, IPipelineBuilder
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

    public IJobCommandHandler<TCommand> Build<TCommand>(IJobCommandHandler<TCommand> innerHandler, IJobExecutionScope scope) 
        where TCommand : IJobCommand
    {
        if (_userMiddlewares.Count + _systemOuterMiddlewares.Count == 0)
            return innerHandler;

        var firstMw = _userMiddlewares.Count > 0 ? _userMiddlewares[^1] : _systemOuterMiddlewares[0];
        
        WrappedHandler<TCommand> wrappedHandler = Wrap(innerHandler, scope, firstMw);
        
        for (int i = _userMiddlewares.Count - 2; i >= 0; i--)
        {
            wrappedHandler = Wrap(wrappedHandler, scope, _userMiddlewares[i]);
        }
        for (int i = _userMiddlewares.Count > 0 ? 0 : 1; i < _systemOuterMiddlewares.Count; i++)
        {
            wrappedHandler = Wrap(wrappedHandler, scope, _systemOuterMiddlewares[i]);
        }

        return wrappedHandler;
    }

    private WrappedHandler<TCommand> Wrap<TCommand>(IJobCommandHandler<TCommand> handler, IJobExecutionScope scope, MiddlewareDefinition middleware)
        where TCommand : IJobCommand
    {
        // think about cache WrappedHandler objects by ObjectPool

        if (middleware.MiddlewareInstance != null)
        {
            return new WrappedHandler<TCommand>(handler, middleware.MiddlewareInstance);
        }
        else if (middleware.MiddlewareType != null)
        {
            return new WrappedHandler<TCommand>(handler, middleware.MiddlewareType, scope);
        }

        throw new InvalidMiddlewaresConfigException("Invalid middleware definition");
    }
}
