# Последовательное выполнение

Jobby предоставляет два способа определять набор задач, которые будут выполняться строго последовательно:

- Создание цепочки задач через `JobsSequenceBuilder`.
- Определение для задач параметра `SerializableGroupId` для объединения задач в группы, задачи которых выполняются строго последовательно.

## JobsSequenceBuilder

Первый способ применим когда вам нужно создать несколько задач для последовательного выполнения сразу в одной транзакции.

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

Задачи созданные через `JobsSequenceBuilder` гарантированно будут выполнены в указанном порядке даже если они были созданы в разных очередях.

В случае провала одной из задач последующие запущены не будут.

## Группы последовательного выполнения

Jobby позволяет при создании задачи указать идентификатор группы в параметре `SerializableGroupId`. Задачи, имеющие один и тот же идентификатор, Jobby будет выполнять строго по одной в порядке возрастания запланированного времени запуска. Jobby гарантирует, что из каждой группы будет одновременно выполняться максимум по одной задаче.

В отличии от первого способа, данный подход можно применять даже когда задачи одной группы создаются в разное время и в разных транзакциях.

Данный способ гарантирует, что задачи одной группы будут выполнены строго по одной, но порядок их запуска по возрастанию запланированного времени запуска гарантируется
только если все задачи группы создаются в одной очереди.

### Идентификатор группы для задачи

Параметр `SerializableGroupId` можно определить на уровне команды, реализовав интерфейс `IHasDefaultJobOptions`:

```csharp
class UpdateOrderCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "UpdateOrderJob";

    public required string OrderId { get; init; }

    // Задачи для одинаковых OrderId
    // будут выполняться последовательно
    public JobOpts GetOptionsForEnqueuedJob()
    {
        return new JobOpts
        {
            SerializableGroupId = OrderId
        }
    }

    public RecurrentJobOpts GetOptionsForRecurrentJob()
    {
        return default;
    }
}
```

Или `SerializableGroupId` можно указать непосредственно при создании конкретного экземпляра задачи:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    SerializableGroupId = command.OrderId
});
```

Аналогично идентификатор группы можно определять и для задач по расписанию.

### Отключение функционала групп последовательного выполнения

Если вы не планируете заполнять в ваших задачах `SerializableGroupId`, то поддержку данного функционала можно отключить с целью повышения производительности библиотеки (но это
не является обязательным):

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseServerSettings(new JobbyServerSettings
            {
                // Отключить группы последовательного выполнения
                DisableSerializableGroups = true
            });
    });
});
```

Флаг `DisableSerializableGroups` можно так же определить на уровне отдельной очереди. Например группы последовательного выполнения можно отключить глобально и включить только
для одной очереди, в которой планируется создание задач с заполненным `SerializableGroupId`:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseServerSettings(new JobbyServerSettings
            {
                // Отключить группы последовательного выполнения глобально
                DisableSerializableGroups = true,
                Queues = [
                    new()
                    {
                        QueueName = "for_groups",

                        // Но включить для отдельной очереди
                        DisableSerializableGroups = false,
                    }
                ]
            });
    });
});
```

Манипуляции с флагом `DisableSerializableGroups` не обязательны, но позволят добиться более оптимального режима работы Jobby в случае, если вы планируете использовать `SerializableGroupId` не для всех задач.

### Блокировка группы при ошибке

По умолчанию группа не блокируется, если одна из её задач была провалена. После провала всех попыток задача перейдёт в статус `Failed` и будет запущена следующая задача из группы.

Это поведение можно изменить для обычных не рекуррентных задач: в случае провала всех попыток группа останется заблокированной и никакие другие задачи из группы запускаться
не будут. Для этого необходимо установить у задачи параметр `JobOpts.LockGroupIfFailed`=`true`:

```csharp
class UpdateOrderCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "UpdateOrderJob";

    public required string OrderId { get; init; }

    public JobOpts GetOptionsForEnqueuedJob()
    {
        return new JobOpts
        {
            SerializableGroupId = OrderId,

            // Если задача будет провалена
            // другие задачи из группы не запустятся
            LockGroupIfFailed = true,
        }
    }

    public RecurrentJobOpts GetOptionsForRecurrentJob()
    {
        return default;
    }
}

// Или непосредственно при создании экземпляра задачи
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    SerializableGroupId = command.OrderId,
    LockGroupIfFailed = true,
});
```

Данную возможность стоит использовать осторожно т.к. в случае создания большого количества готовых к запуску задач в заблокированных на длительное время группах
производительность процесса запуска задач будет снижаться, а нагрузка на базу данных - расти.

### Заморозка и разморозка заблокированных групп

Jobby имеет встроенную возможность по автоматическому обнаружению групп, заблокированных упавшими задачами. В случае обнаружения большого количества готов к запуску задач
в таких заблокированных группах, Jobby будет переводить их в статус `Frozen` чтобы они не оказывали негативного эффекта на производительность процесса запуска задач.

В будущих версиях будет добавлен UI, на котором можно будет увидеть список заблокированных групп и, при необходимости, запустить процесс разблокировки и разморозки задач.

До появления UI данные операции при необходимости можно сделать через SQL-запросы.

```sql
-- Получить id групп заблокированных упавшими задачами
SELECT serializable_group_id FROM jobby_jobs as locker
WHERE 
locker.is_group_locker = true
AND (
    locker.status = 4
    OR (
        locker.status = 2
        AND locker.can_be_restarted = false
        AND NOT EXISTS (
            SELECT 1 FROM jobby_servers as s
            WHERE s.id = locker.server_id
        )
    )
);

-- Запустить разблокировку и разморозку для группы
INSERT INTO jobby_unlocking_groups (group_id, created_id)
VALUES (<id группы для разблокировки>, now());
```