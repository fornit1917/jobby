# Описание фоновых задач

## Команда и обработчик

Для описания фоновой задачи в вашем куда нужно реализовать интерфейс `IJobCommand` для объекта-параметра задачи 
и интерфейс `IJobCommandHandler` непосредственно для кода задачи:

```csharp
public class SendEmailCommand : IJobCommand
{
    // В свойствах можно указать любые параметры,
    // которые будут переданы в задачу
    public string Email { get; init; }

    // Из этого метода нужно вернуть любое уникальное название 
    // идентифицирующее тип задачи
    public static string GetJobName() => "SendEmail";
}

public class SendEmailCommandHandler : IJobCommandHandler<SendEmailCommand>
{
    // При использовании пакета Jobby.AspNetCore 
    // поддерживается внедрение зависимостей из DI-контейнера
    private readonly IEmailService _emailService;
    public SendEmailCommandHandler(IEmailService logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Здесь вы можете написать код, выполняющий вашу задачу
        // command - параметры задачи
        // ctx - содержит cancelationToken и дополнительную информацию
    }
}
```

## Добавление джобов в Jobby

При настройке библиотеки необходимо указать сборки, содержащие код ваших команд и обработчиков:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    // Указываем сборки с командами и обработчиками
    jobbyBuilder
        .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        // ...
    });
});
```

## JobExecutionContext

В метод `ExecuteAsync` помимо объекта команды передаётся объекта класса `JobExecutionContext` со следующими полями:

- **JobName** - Название типа задачи (совпадает со значением, которое вы указали в методе `TCommand.GetJobName()`).
- **StartedCount** - Номер текущей попытки выполнить данную фоновую задачу.
- **IsLastAttempt** - Флаг, указывающий является ли текущая попытка выполнить данную задачу последней согласно используемой политике повторов. Если значение `true`, то задача в случае провала больше не будет перезапущена.
- **IsRecurrent** - Флаг, указывающий на то что текущая задача является задачей по расписанию. 
- **CancellationToken** - CancellationToken, который переходит в статус отмены при остановке сервера, на котором была запущена задача.

## Настройки по умолчанию

В классе команды можно при необходимости реализовать интерфейс `IHasDefaultJobOptions` с методами:

- **GetOptionsForEnqueuedJob()** - возвращает настройки фоновых задач, которые будут использованы по умолчанию при запуске команды *через очередь*.
- **GetOptionsForRecurrentJob()** - возвращает настройки фоновых задач, которые будут использованы по умолчанию при запуске команды в качестве *задачи по расписанию*.

Пример для указания названия очередей, в которых будут создавать фоновые задачи для всех команд данного типа:

```csharp
public class SomeCommand : IJobCommand, IHasDefaultJobOptions
{
    public string SomeParam { get; init; }
    public static string GetJobName() => "SomeJob";

    // Задаём имя очереди, в которой будут создаваться
    // все фоновые задачи для команд SomeCommand
    public JobOpts GetOptionsForEnqueuedJob() => new JobOpts 
    {
        QueueName = "SomeQueue"
    };

    // Задаём имя очереди, в которой будут создаваться
    // все фоновые задачи по расписанию для команд SomeCommand
    public RecurrentJobOpts GetOptionsForRecurrentJob() => new RecurrentJobOpts
    {
        QueueName = "SomeRecurrentJobsQueue"
    };
}
```

Эти методы возвращают структуры `JobOpts` и `RecurrentJobOpts` соответственно.

В структуре `JobOpts` можно переопределить следующие параметры для запускаемых для однократного выполнения задач через очередь:

- **StartTime** - Время, не ранее которого должна быть запущена задача. Подробнее см. [Постановка задач в очередь](./jobs-enqueue).
- **QueueName** - Название очереди для создаваемой задачи. Подробнее см. [Мульти-очереди](./multiqueues).
- **SerializableGroupId** - Идентификатор группы для строго последовательного выполнения входящих в неё задач. По умолчанию `null`. Подробнее см. [Последовательное исполнение](./sequential-execution).
- **LockGroupIfFailed** - Флаг, указывающий на то что указанная в параметре `SerializableGroupId` группа должна остаться заблокированной в случае когда задачу не удалось выполнить. По умолчанию `false`. Подробнее см. [Последовательное исполнение](./sequential-execution).
- **CanBeRestartedIfServerGoesDown** - Флаг, указывающий на то, что задачу допустимо запускать повторно, если исполняющий её сервер предположительно перестал функционировать. По умолчанию `true`. Подробнее см. [Устойчивость к сбоям](./fault-tolerance).

В структуре `RecurrentJobOpts` можно переопределить следующие параметры для запускаемых по расписанию задач:

- **StartTime** - Время, не ранее которого должна быть запущена задача по расписанию в первый раз. Подробнее см. [Задачи по расписанию](./jobs-schedule).
- **QueueName** - Название очереди для создаваемой задачи по расписанию. Подробнее см. [Мульти-очереди](./multiqueues).
- **SerializableGroupId** - Идентификатор группы для строго последовательного выполнения входящих в неё задач. По умолчанию `null`. Подробнее см. [Последовательное исполнение](./sequential-execution).
- **CanBeRestartedIfServerGoesDown** - Флаг, указывающий на то, что задачу допустимо продолжать запускать согласно расписанию, если исполняющий её сервер предположительно перестал функционировать. По умолчанию `true`. Подробнее см. [Устойчивость к сбоям](./fault-tolerance).
- **IsExclusize** - Флаг, указывающий на то, что допустимо создавать более одной задачи по расписанию для указанного в команде `JobName`. По умолчанию `false`. Подробнее см. [Задачи по расписанию](./jobs-schedule).