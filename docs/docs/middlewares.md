# Middlewares

Jobby allows you to wrap background job handler calls with your own Middlewares and build a pipeline from them, similar to the request processing pipeline in AspNetCore.

## Implementation and Registration

To create a Middleware, you need to implement the `IJobbyMiddleware` interface:

```csharp
public class SomeMiddleware : IJobbyMiddleware
{
    // Dependency injection via constructor is supported
    // as in AspNetCore
    private readonly ISomeService _someService;

    public SomeMiddleware(ISomeService someService)
    {
        _someService = someService
    }

    public async Task ExecuteAsync<TCommand>(
        TCommand command, 
        JobExecutionContext ctx, 
        IJobCommandHandler<TCommand> handler) where TCommand : IJobCommand
    {
        // You can place logic here that will execute
        // before the background job is called
        // ....

        await handler.ExecuteAsync(command, ctx);

        // You can place logic here that will execute
        // after the background job is called
        // .... 
    }
}
```

Constructor injection is supported for Middlewares.

Registration:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>  
{
    jobbyBuilder.ConfigureJobby((serviceProvider, jobby) => {
        // ...
        jobby.ConfigurePipeline(pipeline => {

            // This is how you register a singleton middleware without dependencies
            pipeline.Use(new SomeMiddleware());

            // This is how you can register a singleton middleware with non-scoped dependencies
            // In this case, the SomeMiddleware type 
            // must be registered in the DI container!
            pipeline.Use(serviceProvider.GetRequiredService<SomeMiddleware>());

            // This is how you can register scoped middleware or middleware with scoped dependencies
            // In this case, the SomeMiddleware type
            // must be registered in the DI container!
            pipeline.Use<SomeMiddleware>();
        });
    }); 
});
```

## Usage Examples

### Logging

Middlewares can be useful for implementing common logging for all background jobs. Below is the `JobLoggingMiddleware` code, which is added for all background jobs:
- Logging before execution.
- Logging after successful completion.
- Logging in case of failure with separate messages for when the last attempt has failed and when the retry limit has not been exhausted and another attempt will be made.

```csharp
public class JobLoggingMiddleware : IJobbyMiddleware
{
    private readonly ILogger<JobLoggingMiddleware> _logger;

    public JobLoggingMiddleware(ILogger<JobLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync<TCommand>(
        TCommand command,
        JobExecutionContext ctx,
        IJobCommandHandler<TCommand> handler) where TCommand : IJobCommand
    {
        try
        {
            _logger.LogInformation($"Job {ctx.JobName} started");
            await handler.ExecuteAsync(command, ctx);
            _logger.LogInformation($"Job {ctx.JobName} completed");
        }
        catch (Exception ex)
        { 
            if (ctx.IsLastAttempt)
            {
                _logger.LogError(ex, $"Job {ctx.JobName} failed");
            }
            else
            {
                _logger.LogWarning(ex, $"Job {ctx.JobName} failed and will be restarted");
            }
            throw;
        }
    }
}
```

Registering `JobLoggingMiddleware`:

```csharp
builder.Services.AddScoped<JobLoggingMiddleware>();

builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .ConfigurePipeline(pipeline =>
            {   
                pipeline.Use<JobLoggingMiddleware>();
            });
    });
});
```

### Ignoring Specific Errors

By default, Jobby considers a job failed if its handler throws any exception. Let's say you don't want to consider a specific exception type as an error and want the job to be considered completed even if the handler throws that exception.

This behavior can be implemented using the following Middleware:

```csharp
public class IgnoreSomeErrorsMiddleware : IJobbyMiddleware
{
    public async Task ExecuteAsync<TCommand>(
        TCommand command,
        JobExecutionContext ctx,
        IJobCommandHandler<TCommand> handler) where TCommand : IJobCommand
    {
        try
        {
            await handler.ExecuteAsync(command, ctx);
        }
        catch(ExceptionShouldBeIgnored)
        {
        }
    }
}
```

Practical usage of these examples can be found here: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).