# Middlewares

Jobby позволяет оборачивать вызов обработчиков фоновых задач в свои Middlewares и выстраивать из них пайплайн аналогичный пайплайну обработки запросов в AspNetCore.

## Реализация и подключение

Для создания Middleware необходимо реализовать интерфейс `IJobbyMiddleware`:

```csharp
public class SomeMiddleware : IJobbyMiddleware
{
    // В AspNetCore поддерживается
    // внедрение зависимостей через конструктор
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
        // Здесь можно разместить логику 
        // которая выполнится до вызова фоновой задачи
        // ....

        await handler.ExecuteAsync(command, ctx);

        // Здесь можно разместить логику которая выполнится 
        // после вызова фоновой задачи
        // .... 
    }
}
```

Для Middleware поддерживается инъекция зависимостей через конструктор.

Подключение:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>  
{
    jobbyBuilder.ConfigureJobby((serviceProvider, jobby) => {
        // ...
        jobby.ConfigurePipeline(pipeline => {

            // Так подключается singleton middleware без зависимостей
            pipeline.Use(new SomeMiddleware());

            // Так можно подключать singleton middleware с не scoped-зависимостями
            // В этом случае тип SomeMiddleware 
            // должен быть зарегистрирован в DI-контейнере!
            pipeline.Use(serviceProvider.GetRequiredService<SomeMiddleware>());

            // Так можно подключать scoped middleware или middleware со scoped зависимостями
            // В этом случае тип SomeMiddleware
            // должен быть зарегистрирован в DI-контейнере!
            pipeline.Use<SomeMiddleware>();
        });
    }); 
});
```

## Примеры применения

### Логирование

Middlewares могут быть полезны для реализации общего логирования для всех фоновых задач. Ниже приводится код `JobLoggingMiddleware`, который добавляется для всех фоновых задач:
- Логирование перед запуском.
- Логирование после успешного завершения.
- Логирование в случае неуспешного выполнения с отдельными сообщениями для случая когда была провалена последняя попытка выполнить задача и для случая когда лимит повторов еще не исчерпан и будет сделана ещё одна попытка.

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

Подключение `JobLoggingMiddleware`:

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

### Игнор определённых ошибок

По умолчанию Jobby считает задачу проваленной, если её обработчик выбросил любое исключение. Допустим исключение какого-то определённого типа вы не хотите считать ошибкой
и хотите чтобы задача считалась выполненной даже если обработчик выбросил это исключение.

Такое поведение можно реализовать с помощью следующей Middleware:

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

Использование этих примеров на практике можно найти здесь: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).
