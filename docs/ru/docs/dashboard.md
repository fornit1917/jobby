# Дашборд

В Jobby есть необязательный дашборд для просмотра и управления фоновыми задачами в любом ASP.NET Core-хосте: Web API, MVC или Blazor.

Дашборд работает как одностраничное приложение на **Blazor WebAssembly**: хост раздаёт статические файлы, а данные и команды проходят через **JSON API без серверного состояния** (`/jobby/api/*`). Здесь нет SignalR circuit, серверного состояния компонентов и привязки пользователя к конкретной реплике.

## Пакеты

| Пакет | Назначение |
|---|---|
| `Jobby.Dashboard` | Интерфейс на Blazor WebAssembly (встроенные статические файлы) + JSON API; `AddJobbyDashboard()` / `MapJobbyDashboard()` |
| `Jobby.Postgres` | `AddJobbyPostgresDashboardStorage()` - PostgreSQL-хранилище для чтения и команд |
| `Jobby.Dashboard.Authorization.Basic` | Дополнительная встроенная Basic-аутентификация (`AddBasicAuth` / `AddBasicAuthScheme`) и `PasswordHasher` |

`Jobby.Dashboard` использует .NET 8 и зависит от ASP.NET Core 8. Приложениям, использующим NuGet-пакеты, workload сборки WebAssembly не требуется. Удаление пакета и соответствующих вызовов регистрации не меняет поведение движка Jobby.

Устанавливайте `Jobby.Dashboard.Authorization.Basic` только если нужны встроенные вспомогательные методы Basic-аутентификации. Дашборд также может работать анонимно для локальной разработки или использовать аутентификацию и авторизацию, уже настроенные в host-приложении.

`Jobby.Dashboard.UI` - проект реализации интерфейса на WebAssembly. Его собранные ресурсы встроены в `Jobby.Dashboard`; устанавливать его как отдельный NuGet-пакет не требуется.

## Установка

Установите дашборд и зависимости для ASP.NET Core и PostgreSQL в проект хоста:

```bash
dotnet add package Jobby.Core
dotnet add package Jobby.Postgres
dotnet add package Jobby.AspNetCore
dotnet add package Jobby.Dashboard
```

Для встроенных вспомогательных методов Basic-аутентификации дополнительно установите:

```bash
dotnet add package Jobby.Dashboard.Authorization.Basic
```

Используйте одну версию релиза для всех пакетов Jobby. Политики хоста и политики авторизации, заданные inline, работают без пакета Basic.

## Настройка

### 1. Регистрация сервисов (до `builder.Build()`)

`AddJobbyDashboard()` возвращает `IJobbyDashboardBuilder`. Перед вызовом `MapJobbyDashboard` необходимо **ровно один раз** выбрать способ авторизации через возвращённый объект (см. раздел [Безопасность](#безопасность)).

```csharp
IJobbyDashboardBuilder dashboardBuilder = builder.Services.AddJobbyDashboard(o =>
{
    o.RefreshInterval             = TimeSpan.FromSeconds(5);
    o.StaleServerThresholdSeconds = 300;
    o.ReadOnly                    = false;
});

dashboardBuilder.AllowAnonymous();

// Схема и префикс должны совпадать с настройками PostgreSQL движка (см. ниже).
builder.Services.AddJobbyPostgresDashboardStorage(npgsqlDataSource, o =>
{
    o.SchemaName   = "";          // пустая строка -> схема public (по умолчанию)
    o.TablesPrefix = "jobby_";
});
```

`ReadOnly = false` разрешает управляющие действия, если выбранное хранилище также регистрирует хранилище команд. Установите `ReadOnly = true`, если дашборд должен работать только в режиме просмотра.

### 2. Подключение маршрута дашборда (после `app.Build()`)

```csharp
app.MapJobbyDashboard("/jobby");
```

Базовый путь дашборда не должен быть корневым. После этого дашборд будет доступен по адресу `/jobby`.

### Полный пример

Пример разрешает анонимный доступ для локальной разработки. Для защищённого развёртывания замените `AllowAnonymous()` на политику хоста или Basic-аутентификацию. Задайте строку подключения `Jobby` и зарегистрируйте обработчики задач из сборки хоста.

```csharp
using Jobby.AspNetCore;
using Jobby.Core.Interfaces;
using Jobby.Dashboard;
using Jobby.Postgres.ConfigurationExtensions;
using Jobby.Postgres.Dashboard;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Jobby")
    ?? throw new InvalidOperationException("Connection string 'Jobby' was not found.");

await using var dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);

builder.Services.AddJobbyServerAndClient(jobby =>
{
    jobby.AddJobsFromAssemblies(typeof(Program).Assembly);
    jobby.ConfigureJobby((sp, config) =>
    {
        config.UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>());
    });
});

builder.Services
    .AddJobbyDashboard()
    .AllowAnonymous();

builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.SchemaName   = "";
    o.TablesPrefix = "jobby_";
});

var app = builder.Build();

app.Services.GetRequiredService<IJobbyStorageMigrator>().Migrate();

app.MapJobbyDashboard("/jobby");
app.Run();
```

## Безопасность

**Дашборд защищён по умолчанию.** `MapJobbyDashboard` выбрасывает исключение при запуске, если через `IJobbyDashboardBuilder` не был выбран ровно один способ авторизации. Если не выбрать способ авторизации или выбрать два, приложение завершит запуск с исключением.

Статические файлы Blazor WebAssembly-приложения и файлы фреймворка **всегда доступны без авторизации** - они не содержат секретов и должны загрузиться в браузер до старта приложения. Выбранная политика защищает только JSON API (`/jobby/api/*`). Это поведение сохраняется даже при глобальном `AuthorizationOptions.FallbackPolicy`: для статических ресурсов явно указан `AllowAnonymous`, а для API - `RequireAuthorization`.

> **Не забудьте подключить middleware аутентификации/авторизации.** Дашборд защищает API через метаданные маршрутов (`RequireAuthorization`) и не добавляет middleware в конвейер сам. Со стандартной минимальной моделью хостинга всё работает из коробки: `AddJobbyDashboard` регистрирует сервисы аутентификации/авторизации, а `WebApplication` автоматически добавляет `UseAuthentication`/`UseAuthorization`. Если вы строите конвейер middleware вручную (например, через `UseRouting`/`UseEndpoints`), вызовите `app.UseAuthentication()` и `app.UseAuthorization()` до маршрутов дашборда - без middleware авторизации метаданные API не применяются.

Выберите ровно один вариант:

### Вариант 1 - Анонимный доступ (только для локальной разработки)

```csharp
builder.Services.AddJobbyDashboard().AllowAnonymous();
```

### Вариант 2 - Повторное использование существующей политики хоста

Сохраните существующую регистрацию аутентификации и именованные политики авторизации хоста. Для этого варианта достаточно `Jobby.Dashboard`, пакет Basic не нужен.

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireHostPolicy("MyReadPolicy");
    // дополнительная перегрузка: .RequireHostPolicy("MyReadPolicy", "MyManagePolicy")
```

### Вариант 3 - Настройка политики в коде

```csharp
builder.Services
    .AddJobbyDashboard()
    .RequireAuthorization(
        read   => read.RequireRole("jobby-read"),
        manage => manage.RequireRole("jobby-manage")
    );
```

### Вариант 4 - Встроенная Basic-аутентификация (`Jobby.Dashboard.Authorization.Basic`)

Для сервисов без собственной системы аутентификации дополнительный пакет `Jobby.Dashboard.Authorization.Basic` предоставляет Basic-схему с одной учётной записью, где пароль хранится в виде PBKDF2-хеша.

```csharp
using Jobby.Dashboard.Authorization.Basic;

builder.Services
    .AddJobbyDashboard()
    .AddBasicAuth(o =>
    {
        o.Username     = "admin";
        o.PasswordHash = builder.Configuration["JobbyDashboard:PasswordHash"]
            ?? throw new InvalidOperationException("JobbyDashboard:PasswordHash is required.");
    });

app.MapJobbyDashboard("/jobby");
```

`AddBasicAuth` одновременно регистрирует схему Basic-аутентификации **и** настраивает авторизацию дашборда. Не сочетайте его с другими методами выбора авторизации.

**Сгенерируйте хеш пароля** и сохраните его в конфигурации хоста как `JobbyDashboard:PasswordHash` (никогда не храните пароль в открытом виде):

```csharp
string hash = Jobby.Dashboard.Authorization.Basic.PasswordHasher.Hash("ваш-пароль");
```

`PasswordHasher.Hash` возвращает строку вида `JDPBKDF2$v1$...` (PBKDF2-HMAC-SHA256). `AddBasicAuth` выбрасывает исключение при запуске, если переданный `PasswordHash` не является корректной PBKDF2-кодировкой.

> **Требуется HTTPS:** Basic-аутентификация передаёт учётные данные в base64-кодировке. В рабочей среде дашборд всегда должен работать через HTTPS.

### Вариант 5 - Совместное использование Basic и JWT (продвинутый)

Используйте `AddBasicAuthScheme`, чтобы зарегистрировать Basic-схему без выбора политики авторизации, а затем вызовите `RequireAuthorization`, чтобы объединить её с Bearer:

```csharp
using Jobby.Dashboard.Authorization.Basic;
using Microsoft.AspNetCore.Authentication.JwtBearer;

builder.Services
    .AddJobbyDashboard()
    .AddBasicAuthScheme(o =>
    {
        o.Username     = "admin";
        o.PasswordHash = builder.Configuration["JobbyDashboard:PasswordHash"]
            ?? throw new InvalidOperationException("JobbyDashboard:PasswordHash is required.");
    })
    .RequireAuthorization(p =>
        p.AddAuthenticationSchemes(
                JobbyDashboardBasicDefaults.Scheme,
                JwtBearerDefaults.AuthenticationScheme)
         .RequireAuthenticatedUser());
```

Зарегистрируйте схему Bearer в хосте перед использованием этой комбинации.

При запросе без аутентификации JSON API отвечает `401` с заголовками `WWW-Authenticate: Basic` и `WWW-Authenticate: Bearer` одновременно - браузеры реагируют на `Basic`, программные клиенты - на `Bearer`.

## Соответствие схемы и префикса

Параметры (`SchemaName`, `TablesPrefix`), переданные в `AddJobbyPostgresDashboardStorage`, **должны совпадать** с настройками, переданными в `UsePostgresql(...)` при конфигурировании движка. Несоответствие приведёт к тому, что дашборд будет обращаться к неверным или несуществующим таблицам.

```csharp
config.UsePostgresql(pg =>
{
    pg.UseDataSource(dataSource);
    pg.UseSchemaName("jobs");
    pg.UseTablesPrefix("jobby_");
});

builder.Services.AddJobbyPostgresDashboardStorage(dataSource, o =>
{
    o.SchemaName   = "jobs";
    o.TablesPrefix = "jobby_";
});
```

## Настройки отображения

Через `JobbyDashboardOptions` можно задать начальный часовой пояс, формат даты и времени, а также язык интерфейса. Клиент получает эти значения из `GET /jobby/api/config` при запуске.

Пользователь может поменять те же настройки в верхней панели дашборда. Выбор сохраняется в `localStorage` и используется вместо настроек сервера, пока пользователь не сбросит его.

```csharp
using Jobby.Core.Models.Dashboard;

builder.Services
    .AddJobbyDashboard(o =>
    {
        o.DefaultTimeZone   = TimeZoneInfo.Utc;
        o.DefaultDateStyle  = DashboardDateStyle.Iso;
        o.DefaultTimeStyle  = DashboardTimeStyle.H24WithSeconds;
        o.DefaultLanguage   = DashboardLanguage.English;
    })
    .AllowAnonymous();
```

`DefaultTimeZone` по умолчанию равен `TimeZoneInfo.Utc`. Если нужен другой пояс, используйте `TimeZoneInfo.FindSystemTimeZoneById(...)`. API возвращает время в UTC. Интерфейс показывает его в выбранном часовом поясе и переводит фильтры по датам обратно в UTC перед запросом.

В `/api/config` также передаётся `ServerTimeUtc`. Клиент использует его для относительных значений вроде «5 минут назад», чтобы они не зависели от часов в браузере.

## Возможности и API

Дашборд обновляет данные опросом с интервалом `RefreshInterval` и показывает:

- **Обзор** - актуальное количество задач по статусу и очереди.
- **Задачи** - список задач с пагинацией, фильтрами и сортировкой.
- **Детали задачи** - параметры, ошибка, временные метки и другие поля записи.
- **Задачи по расписанию** - рекуррентные задачи, их расписание и ближайший запуск.
- **Серверы** - подключённые экземпляры Jobby-сервера и состояние heartbeat.
- **Очереди** - разбивка по статусам для каждой очереди.
- **Заблокированные группы** - группы последовательного выполнения, остановленные из-за упавших или потерянных задач.

Управляющие действия доступны, когда зарегистрирован `IJobbyDashboardCommandStorage`, а `JobbyDashboardOptions.ReadOnly` равно `false`. `AddJobbyPostgresDashboardStorage()` регистрирует и хранилище для чтения, и хранилище команд. Если управление отключено, дашборд скрывает соответствующие действия, а API-маршруты управления возвращают `404`. Режим «Только чтение» в верхней панели скрывает элементы управления для удобства; ограничивайте доступ на сервере через `JobbyDashboardOptions.ReadOnly` или политики авторизации.

### Даты планового запуска и сортировка

Фильтры От/До в списке задач выбирают дату планового запуска в часовом поясе, выбранном в дашборде. Обе календарные даты включены; интерфейс переводит их границы в UTC с учётом переходов на летнее и зимнее время. Если начало диапазона позже конца, интерфейс показывает ошибку и сохраняет весь последний корректный результат до исправления. Изменения фильтров статуса, очереди или поиска, сделанные при некорректном диапазоне, применяются после его исправления. Время создания доступно для просмотра и сортировки.

По умолчанию список задач и вкладка всех задач очереди отсортированы по плановому запуску по возрастанию: сначала идут самые ранние задачи. Вкладка незавершённых задач очереди использует тот же порядок. Пользователь может выбрать другой столбец и направление сортировки.

API задач принимает включительные границы UTC `scheduledFrom` и `scheduledTo`; им соответствуют свойства `ScheduledFrom` и `ScheduledTo` в `DashboardJobsQuery`. Диапазон относится к текущему плановому запуску, включая повторы и перепланирование, а не ко времени создания или фактического выполнения.

### Досрочный запуск запланированных задач

Действие запуска доступно только для задач в статусе `Scheduled`, включая задачи по расписанию. Оно переносит будущий плановый запуск на текущее время, а уже наступивший оставляет без изменений. Рабочий сервер выполнит задачу, когда сможет взять её по обычным правилам очередей, серверов и групп последовательного выполнения; HTTP-запрос сам обработчик не выполняет.

Задача по расписанию запускается в той же записи с тем же ID, без создания отдельной разовой копии. После выполнения обычный планировщик рассчитывает следующий запуск, поэтому интервальное расписание может сдвинуться. Например, часовая задача с плановым запуском в 12:00, запущенная досрочно в 11:10 и завершившаяся в 11:12, может затем запуститься в 12:10 или 12:12 в зависимости от `CalculateNextFromPrev`. Cron-расписание использует следующее подходящее время. Повторные запросы, пока запуск ожидается, не создают дополнительных копий; запрос после завершения может приблизить следующий запуск.

### Уникальность задач по расписанию

В списке задач по расписанию и в сведениях о задаче отображается признак «Уникальная по имени» - флаг `IsExclusive` (`is_exclusive` в хранилище). В разделе [Задачи по расписанию](./jobs-schedule) такая задача называется эксклюзивной. Для одного имени задачи может существовать только одна задача с этим флагом; задачи без флага с тем же именем могут существовать одновременно. Флаг управляет уникальностью задач по расписанию, а не взаимным исключением выполнений; чтобы задачи выполнялись строго по очереди, используйте группы последовательного выполнения.

### API чтения

Маршруты чтения доступны под базовым путём дашборда:

| Маршрут | Описание |
|---|---|
| `GET /jobby/api/config` | Конфигурация дашборда, которую клиент запрашивает при старте |
| `GET /jobby/api/stats` | Агрегированная статистика по задачам |
| `GET /jobby/api/jobs` | Постраничный список задач (параметры: `status`, `statuses`, `queueName`, `jobNameSearch`, `scheduledFrom`, `scheduledTo`, `page`, `pageSize`, `sortBy`, `sortDescending`) |
| `GET /jobby/api/jobs/{id}` | Детали одной задачи |
| `GET /jobby/api/recurrent` | Постраничный список задач по расписанию |
| `GET /jobby/api/servers` | Зарегистрированные экземпляры Jobby-сервера |
| `GET /jobby/api/queues` | Статистика по очередям |
| `GET /jobby/api/locked-groups` | Постраничный список заблокированных групп |
| `GET /jobby/api/locked-groups/detail?groupId={id}` | Детали заблокированной группы и связанных задач |

### API управления

Маршруты управления проверяют antiforgery-заголовок и токен, полученные из `/jobby/api/config`:

| Маршрут | Описание |
|---|---|
| `POST /jobby/api/jobs/{id}/trigger` | Запросить досрочный запуск запланированной задачи, включая задачи по расписанию |
| `POST /jobby/api/jobs/{id}/retry` | Повторно запустить упавшую или замороженную задачу (кроме задач по расписанию) |
| `DELETE /jobby/api/jobs/{id}` | Удалить задачу, которая не выполняется прямо сейчас (кроме задач по расписанию) |
| `DELETE /jobby/api/recurrent/{id}` | Удалить задачу по расписанию |
| `POST /jobby/api/locked-groups/unlock-request` | Запросить разблокировку и разморозку группы последовательного выполнения |

Неизвестные пути в рамках `/jobby/api/*` возвращают `404`.

## Хостинг в Kubernetes / несколько реплик

Дашборд является Blazor WebAssembly-приложением **без серверного состояния** и работает через JSON API **без серверного состояния**. Так как он раздаётся как статическое WebAssembly-приложение, а не работает как интерактивное серверное Blazor-приложение:

- **Нет SignalR circuit.** Поддерживать постоянное WebSocket-соединение не требуется.
- **Нет привязки сессии к реплике.** Запросы можно свободно балансировать между репликами без sticky sessions / session affinity.
- **Нет состояния компонентов на сервере.** Дашборд не хранит UI-состояние в процессе ASP.NET Core.

Обычные требования для рабочей среды по-прежнему актуальны:

- **HTTPS** - обязателен при использовании Basic-аутентификации (учётные данные передаются в base64-кодировке, без шифрования).
- **Обратный прокси и хостинг на подпути** - настройте `UseForwardedHeaders` и базовый путь прокси как обычно. Дашборд динамически подставляет корректный `<base href>`, поэтому хостинг на подпути работает без дополнительной ручной настройки.
- **Согласованность данных** - все данные читаются из общей базы данных PostgreSQL, поэтому каждая реплика возвращает одинаковое актуальное состояние кластера.
- **Antiforgery / Data Protection** - управляющие запросы используют ASP.NET Core antiforgery. Если управление включено, а запросы могут попадать на разные реплики, настройте общее хранилище ключей ASP.NET Core Data Protection так же, как для любого ASP.NET Core-приложения с antiforgery.

## Что дальше

- [Установка и настройка](./install-and-config)
- [Устойчивость к сбоям](./fault-tolerance)
- [Метрики и трейсинг](./observability)
