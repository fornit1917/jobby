# Устойчивость к сбоям

Jobby позволяет в ряде случаев пережить отказ некоторых компонентов и продолжить корректную работу без потери данных и без необходимости ручного вмешательства.

## Отказ базы данных

В случае недоступности базы данных Jobby будет повторять следующие операции, которые завершились с ошибкой:

- Получение очередных готовых к запуску задач.
- Запись в БД обновлённых статусов завершённых или проваленных задач.

При восстановлении связи с БД Jobby завершит операции, которые ранее не удавалось выполнить, и вернётся к обычному режиму работу.

Повтор операций в случае ошибок БД производится с интервалом, которые указан в параметре `JobbyServerSettings.DbErrorPauseMs`. Значение по умолчанию 5000 мс, может быть
переопределено следующим образом при настройке библиотеки:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            // ...
            .UseServerSettings(new JobbyServerSettings
            {
                // Повторять запросы к БД 
                // завершившиеся с ошибкой раз в 10 секунд
                DbErrorPauseMs = 10000
            });
    });
});
```

## Отказ Jobby-сервера

### Перезапуск задач с упавших инстансов

Если Jobby-сервер запущен в нескольких экземплярах, то в случае отказа одного из них задачи продолжат выполняться на оставшихся инстансах.

Но что будет с теми задачами, которые были запущены и не были завершены до аварийного отключения того инстанса, на котором они были запущены?

В Jobby реализован механизм Hertbeat для автоматического обнаружения выбывших узлов. Задачи, которые были запущены на выбывших узлах, будут автоматически
перезапущены на живых инстансах. 

Если ваша фоновая задача не является идемпотентной и её автоматический повторный запуск может привести к нежелательным последствиям, то её перезапуск в такой ситуации
можно отключить через флаг `JobOpts.CanBeRestartedIfServerGoesDown` (одноимённый флаг предусмотрен и для задач по расписанию в структуре `RecurrentJobOpts`).
Если этот флаг установлен в `false`, то задача выполнявшаяся на выбывшем узле не будет перезапускаться автоматически.

Переопределить значение этого флага можно при создании конкретного экземпляра фоновой задачи:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    CanBeRestartedIfServerGoesDown = false
});
```

Или его можно переопределить по умолчанию для всех задач определенного типа на уровне класса команды, реализовав `IHasDefaultJobOptions`:

```csharp
class MyCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "MyJob";

    public JobOpts GetOptionsForEnqueuedJob()
    {
        return new JobOpts
        {
            CanBeRestartedIfServerGoesDown = false
        }
    }

    public RecurrentJobOpts GetOptionsForRecurrentJob()
    {
        return new RecurrentJobOpts()
        {
            CanBeRestartedIfServerGoesDown = false
        }
    }
}
```

### Настройки Heartbeat

Механизм Heartbeat имеет несколько настроек в `JobbyServerSettings`:

- **HeartbeatIntervalSeconds** - интервал в секундах, с которым каждый Jobby-сервер отправляет контрольный сигнал о том, что он находится в рабочем состоянии. Значение по умолчанию - **10 секунд**.
- **MaxNoHeartbeatIntervalSeconds** - максимальное количество секунд, в течение которого сервер, не приславший ни одного контрольного сигнала, начинает считаться упавшим. Значение по умолчанию - **300 секунд**.

Эти параметры при необходимости могут быть переопределены при конфигурации библиотеки:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            // ...
            .UseServerSettings(new JobbyServerSettings
            {
                // Отправлять heartbeat-сигналы
                // каждые 5 секунд
                HeartbeatIntervalSeconds = 5,

                // Если от сервера не получено
                // ни одного сигнала за 60 секунд
                // то считать его не рабочим
                MaxNoHeartbeatIntervalSeconds = 60,
            });
    });
});
```
