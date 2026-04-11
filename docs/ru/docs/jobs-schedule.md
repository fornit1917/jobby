# Задачи по расписанию

## Определение

Задачи по расписанию (или _рекуррентные_ задачи) - это такие задачи, которые после создания выполняются не один раз,
а запускаются регулярно согласно указанному расписанию.

Jobby поддерживает расписания в формате _сron_ или в формате простых _интервалов_, а также имеется возможность
реализации собственных планировщиков с расписанием любого формата.

Jobby гарантирует, что задачи по раписанию запускаются без дублей. Например если задача запланирована
к запуску каждые 5 секунд, но выполняется при этом 10 секунд, Jobby не будет запускать новый экземпляр этой задачи
пока не завершится предыдущий.

Задачи по расписанию описываются так же, как и рассмотренные ранее: в виде класса-команды и класса-обработчика:

```csharp
// Код задачи по расписанию описывается аналогично обычной задаче

public class RecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "SomeRecurrentJob";
}

public class RecurrentJobHandler : IJobCommandHandler<RecurrentJobCommand>
{
    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Код вашей задачи по расписанию
    }
}
```

## Cron

Для создания задачи с расписанием в формате cron используйте метод `IJobbyClient.ScheduleRecurrentAsync` (или его синхронную версию):

```csharp
// Установка расписания для задачи (используется cron expression)
// После этого задача будет автоматически запускаться каждые 5 минут
var command = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *");

// Поддерживаются также cron expressions с секундной точностью
// Создание задачи с запуском каждые 5 секунд:
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * * *");

// При необходимости можно передать структуру RecurrentJobOpts
// Например так можно создать задачу по расписанию
// с первым запуском через час, а затем каждые 5 минут
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *", new RecurrentJobOps
{
    StartTime = DateTime.UtcNow.AddHours(1)
});
```

## Интервалы

Кроме формата cron для расписания можно использовать простые интервалы в формате `TimeSpan`.

Например, так можно создать задачу, которая запустится сразу при создании, а затем будет запускаться каждую минуту:

```csharp
var command = new RecurrentJobCommand();
var schedule = new TimeSpanSchedule
{
    Interval = TimeSpan.FromMinutes(1)
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

После завершения задачи время следующего запуска будет расчитано относительно времени завершения. Т.е. если задача была запущена в 00:00:00 и завершилась в 00:00:30, то следующий раз она будет запущега через минуту после завершения, т.е.
в 00:01:30.

Это поведение можно изменить, чтобы время следующего запуска рассчитывалось относительно запланированного _предыдущего_ времени запуска. Тогда задача, запущенная в 00:00:00 и завершившаяся в 00:00:30 в следующий раз запустится через минуту не после завершения, а через минуту после прошлого запуска, а именно в 00:01:00. За это отвечает флаг `CalculateNextFromPrev` в объекте расписания:

```csharp
var command = new RecurrentJobCommand();
var schedule = new TimeSpanSchedule
{
    Interval = TimeSpan.FromMinutes(1),

    // Время следующего запуска
    // будет вычислено отночительно времени предыдущего старта
    // а не завершения
    CalculcateNextFromPrev = true
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

## Произвольный планировщик

В тех случаях, когда вам не хватает возможностей cron или интервалов, Jobby позволяет реализовать собственную логику определения времени запуска задач по расписанию с произвольным форматом раписания и произвольным набором параметров.

Для этого нужно определелить класс для объектов с параметрами расписания, реализующий маркерный интерфейс `ISchedule`:

```csharp
class MySpecialSchedule : ISchedule
{
    // В полях класса можно разместить любые параметры
    // определяющее ваше расписание
    public string CustomSchedule { get; init; }
}
```

Затем для вашего расписания необходимо реализовать класс-обработчик, наследующий абстрактный класс `BaseScheduleHandler<TSchedule>` и реализующий три метода:

- **GetSchedulerTypeName** - Возвращает название, идентифицирующее тип вашего планировщика.
- **GetFirstStartTime** - Возвращает время в UTC для первого запуска задачи (вызывается при создании)
- **GetNextStartTime** - Возвращает время в UTC для следующего запуска задачи (вызывается после очередного исполнения задачи)

```csharp
class MySpecialScheduleHandler : BaseScheduleHandler<MySpecialSchedule>
{
    // Название планировщика
    public override string GetSchedulerTypeName() => "MY_SPECIAL_SCHEDULE";

    // Время для первого запуска
    public override DateTime GetFirstStartTime(TimeSpanSchedule schedule, DateTime utcNow)
    {
        // schedule - объект с параметрами вашего раписания
        // utcNow - текущее время UTC
    }

    public override DateTime GetNextStartTime(TimeSpanSchedule schedule, ScheduleCalculationContext ctx)
    {
        // schedule - объект с параметрами вашего раписания
        // ctx - содержит текущее время и время, в которое был запланирован предыдущий запуск
    }    
}
```

Затем планировщик нужно зарегистрировать при конфигурции библиотеки:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder
        .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>())
            
            // Регистрация собственного планировщика
            .UseScheduler(new MySpecialScheduleHandler());
    });
});
```

Теперь вы можете создавать задачи по расписанию с вашей собственной логикой расчёта времени запуска:

```csharp
var command = new RecurrentJobCommand();
var schedule = new MySpecialSchedule
{
    // ...
    // ...
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

## Эксклюзивность

По умолчанию в Jobby задачи по расписанию являются уникальными по значению `GetJobName()` вашей команды.
Т.е. для одного `JobName` можно создать **только одну** задачу по расписанию. Если задача по расписанию уже существует,
то вызов метода `ScheduleRecurrent` просто обновит у неё расписание.

Пример:

```csharp
// Создаём первую задачу по расписанию для RecurrentJobCommand
var command1 = new RecurrentJobCommand();

// Будет создана задача с запускjм каждые 3 минуты
await jobbyClient.ScheduleRecurrentAsync(command1, "*/3 * * * *");

// Создаём еще одну задачу такого же типа
var command2 = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command2, "*/5 * * * *");

// Вторая задача не будет создана!!!!
// Т.к. уже есть рекуррентная задача с таким же JobName
// Вместо этого обновится расписание у первой задачи
```

Но при необходимости проверку уникальности можно отключить через флаг `IsExclusive`=`false` в структуре
`RecurrentJobOpts`. В примере ниже показано как создать две одинаковые задачи по расписанию, первая будет
запускаться каждые 3 секунды, а вторая - каждые 5 секунд.

```csharp
var command1 = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command1, "*/3 * * * *", new RecurrentJobOpts
{
    IsExclusive = false
});

var command2 = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command2, "*/5 * * * *", new RecurrentJobOpts
{
    IsExclusive = false
});
```

## Отмена задачи по расписанию

Созданная задача по расписанию может быть отменена в любой момент, после чего её запуск прекратится.

Для отмены эксклюзивной задачи по расписанию достаточно указать класс команды в методе **CancelRecurrentAsync**. Важно: данный метод применим только для
эксклюзивных задач по раписанию. Пример

```csharp
// Создать эксклюзивную задачу по расписанию
var command = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * * *");

// Отменить эксклюзивную задачу по расписанию
await jobbyClient.CancelRecurrentAsync<RecurrentJobCommand>();
```

Для отмены не эксклюзивной задачи по расписанию необходимо получить id её экземпляра при создании и затем использовать его для отмены с помощью метода
**CancelRecurrentByIds**:

```csharp
// Создать не эксклюзивную задачу по расписанию
var jobId = await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *", new RecurrentJobOpts
{
    IsExclusive = false
});

// Отменить не эксклюзивную задачу по расписанию
await jobbyClient.CancelRecurrentByIds(jobId);
```