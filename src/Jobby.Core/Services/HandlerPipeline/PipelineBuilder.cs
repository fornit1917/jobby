using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.Configuration;
using Jobby.Core.Interfaces.HandlerPipeline;

namespace Jobby.Core.Services.HandlerPipeline;

internal class PipelineBuilder : IPipelineConfigurable, IPipelineBuilder
{
    private readonly List<MiddlewareDefinition> _middlewareDefinitions = new(); 

    public IPipelineConfigurable Use(IJobbyMiddleware middleware)
    {
        _middlewareDefinitions.Add(new MiddlewareDefinition { MiddlewareInstance = middleware });
        return this;
    }

    public IPipelineConfigurable Use<TMiddleware>() where TMiddleware : IJobbyMiddleware
    {
        _middlewareDefinitions.Add(new MiddlewareDefinition { MiddlewareType = typeof(TMiddleware) });
        return this;
    }

    public IJobCommandHandler<TCommand> Build<TCommand>(IJobCommandHandler<TCommand> innerHandler, IJobExecutionScope scope) 
        where TCommand : IJobCommand
    {
        if (_middlewareDefinitions.Count == 0)
            return innerHandler;

        WrappedHandler<TCommand> wrappedHandler = Wrap(innerHandler, scope, _middlewareDefinitions[^1]);
        for (int i = _middlewareDefinitions.Count - 2; i >= 0; i--)
        {
            wrappedHandler = Wrap(wrappedHandler, scope, _middlewareDefinitions[i]);
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
