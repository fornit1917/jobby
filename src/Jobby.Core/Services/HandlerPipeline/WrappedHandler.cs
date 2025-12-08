using Jobby.Core.Exceptions;
using Jobby.Core.Interfaces;
using Jobby.Core.Interfaces.HandlerPipeline;
using Jobby.Core.Models;

namespace Jobby.Core.Services.HandlerPipeline;

internal class WrappedHandler<TCommand> : IJobCommandHandler<TCommand> where TCommand : IJobCommand
{
    private readonly IJobCommandHandler<TCommand> _innerHandler;
    
    private readonly IJobbyMiddleware? _middleware;
    private readonly Type? _middlewareServiceType;
    private readonly IJobExecutionScope? _scope;

    public WrappedHandler(IJobCommandHandler<TCommand> handler, IJobbyMiddleware middleware)
    {
        _innerHandler = handler;
        _middleware = middleware;
    }

    public WrappedHandler(IJobCommandHandler<TCommand> handler, Type middlewareServiceType, IJobExecutionScope scope)
    {
        _innerHandler = handler;
        _middlewareServiceType = middlewareServiceType;
        _scope = scope;
    }

    public Task ExecuteAsync(TCommand command, JobExecutionContext ctx)
    {
        if (_middleware != null)
        {
            return _middleware.ExecuteAsync(command, ctx, _innerHandler);
        }
        
        if (_middlewareServiceType != null && _scope != null)
        {
            var middleware = _scope.GetService(_middlewareServiceType) as IJobbyMiddleware;
            if (middleware == null)
            {
                throw new InvalidMiddlewaresConfigException($"Could not create instance of IJobbyMiddleware with type {_middlewareServiceType}. Ensure that this type added to your DI Container of Factory");
            }
            return middleware.ExecuteAsync(command, ctx, _innerHandler);
        }

        return _innerHandler.ExecuteAsync(command, ctx);
    }
}
