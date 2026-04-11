# Запуск фоновых задач через очередь

Jobby позволяет отправлять команды в очередь для однократного выполнения их обработчиков в фоне.

## Запуск через IJobbyClient

Для добавления задач в очередь можно использовать сервис `IJobbyClient`, экземпляр которого в случае использования Jobby.AspNetCore можно получить
из DI-контейнера, или из объекта `JobbyBuilder` в ином случае (подробнее см. [Установка и настройка](./install-and-config)).

### Запуск одной задачи

```csharp
var command = new SendEmailCommand 
{ 
    Email = "some@email.com"
};

// Добавить задачу в очередь для выполнения так скоро, насколько возможно
await jobbyClient.EnqueueCommandAsync(command); 

// Добавить задачу в очередь для выполнения не ранее указанного времени
await jobbyClient.EnqueueCommandAsync(command, DateTime.UtcNow.AddHours(1));

// Или аналогично можно использовать синхронные версии методов
jobbyClient.EnqueueCommand(command);
```

Указанные выше методы позволяют опционально передать структуру `JobOpts` - дополнительные настройки, с которыми будет создана фоновая задача. Входящие в эту структуру параметры
описаны на странице [Описание задач](./jobs-definition).

Параметры `JobOpts`, переданные в метод `EnqueueCommand`, имеют более высокий приоритет, чем параметры определенные на уровне класса команды при реализации интерфейса `IHasDefaultJobOptions`. Рассмотрим на примере, как объединяются параметры, определённые на разных уровнях:

```csharp
class SomeCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "SomeJob";

    // Опции на уровне класса команды
    public JobOpts GetOptionsForEnqueuedJob() => new JobOpts
    {
        QueueName = "queue_1",
        CanBeRestartedIfServerGoesDown = false
    }
}

// Добавление задачи в очередь
// с переопределением названия очереди
var command = new SomeCommand();
var opts = new JobOpts
{
    QueueName = "queue_2",
}

await jobbyClient.EnqueueCommandAsync(command, opts); 
```

В итоге будет создана фоновая задача со следующими опциями:

- `QueueName` = `"queue_2"` т.к. этот параметр был переопределён в методе `Enqueue`
- `CanBeRestartedIfServerGoesDown` = `false` т.к. этот параметр определён на уровне команды и не переопределялся в методе `Enqueue`


### Запуск нескольких задач

Для добавления в одной транзакции нескольких задач нужно подготовить для них список объектов `JobCreationModel` с помощью фабрики, доступной в `jobbyClient.Factory`, а затем вызвать метод `EnqueueBatchAsync`:

```csharp
var jobs = new List<JobCreationModel>
{
    jobbyClient.Factory
        .Create(new SendEmailCommand { Email = "first@email.com" }),
    
    jobbyClient.Factory
        .Create(new SendEmailCommand { Email = "second@email.com" }),
}

await jobbyClient.EnqueueBatchAsync(jobs);
```

Метод `Create` аналогично методу `Enqueue` может принимать параметр `JobOpts`.

При добавлении нескольких задач существует опция настроить для них строгий порядок выполнения:

```csharp
var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();

// Задачи будут выполнены в строго указанном ниже порядке
sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "first@email.com" }));

sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "second@email.com" }));

var jobs = sequenceBuilder.GetJobs();

await jobbyClient.EnqueueBatchAsync(jobs);
```

### Группы задач с последовательным выполнением

При создании задачи можно задать идентификатор группы, и Jobby будет гарантировать, что в каждой группе в один момент времени выполняется не более одной задачи.

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts 
{
    SerializableGroupId = "SomeGroupId"
});
```

Следующая задача группы запустится только после успешного или не успешного завершения текущей задачи. Если при неуспешном
завершении необходимо блокировать запуск любых задач из той же группы, то при создании задачи нужно установить
флаг `LockGroupIfFailed`:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts 
{
    SerializableGroupId = "SomeGroupId",
    LockGroupIfFailed = true
});
```

Подробнее о данной возможности см. [Последовательное исполнение](./sequential-execution).

## Запуск через ORM

Если в вашем проекте используется EntityFramework (или иная ORM), то вы можете для добавления задач использовать ваш DbContext.
Это может быть полезно, если в одной транзакции вам необходимо и поставить задачу в очередь, и выполнить операцию
над некоторой доменной сущностью.

Для этого нужно:

- Добавить в DbContext сущность JobCreationModel
- Настроить для сущности название таблицы `jobby_jobs` и указать поле `Id` в качестве первичного ключа
- Применить нужный naming convention (в случае PostgreSQL - snake_case)

Теперь создание фоновых задач можно делать следующим образом:
- C помощью `jobbyClient.Factory` создавать объекты `JobCreationModel`
- Сохранять их в БД через стандартные методы DbConext

```csharp
public class YourDbContext : DbContext
{
    // Нужно добавить DbSet для сущности JobCreationModel
    public DbSet<JobCreationModel> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<JobCreationModel>()
            .ToTable("jobby_jobs");

        modelBuilder
            .Entity<JobCreationModel>()
            .HasKey(x => x.Id);

        // Останется применить snake_case naming convention
        // ...
    }
}

// Постановка задачи в очередь через EF
var command = new SendEmailCommand { Email = "some@email.com" };
var jobEntity = jobbyClient.Factory.Create(command);
_dbContext.Jobs.Add(job);
await _dbContext.SaveChangesAsync();
```

Пример использования EF доступен здесь: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).

## Отмена задачи

Задачу, добавленную в очередь, можно отменить, если она ещё не была запущена.

```csharp
var startTime = DateTime.UtcNow.AddHours(1);

// Создать задачу
// и получить id её экземпляра
var jobId = await jobbyClient.EnqueueCommandAsync(command, startTime);

// Отменить задачу по id экземпляра
await jobbyClient.CancelJobsByIdsAsync(jobId);
```