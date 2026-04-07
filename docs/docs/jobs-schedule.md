# Scheduled Jobs

## Definition

Scheduled tasks (or _recurrent_ tasks) are tasks that, once created, do not run just once but are executed regularly according to a specified schedule.

Jobby supports schedules in _cron_ format or in simple _interval_ format, and also provides the ability to implement custom schedulers with schedules in any format.

Jobby guarantees that scheduled tasks run without duplicates. For example, if a task is scheduled to run every 5 seconds but takes 10 seconds to execute, Jobby will not start a new instance of this task until the previous one has completed.

Scheduled tasks are defined in the same way as previously discussed: as a command class and a handler class:

```csharp
// Scheduled task code is defined similarly to a regular task

public class RecurrentJobCommand : IJobCommand
{
    public static string GetJobName() => "SomeRecurrentJob";
}

public class RecurrentJobHandler : IJobCommandHandler<RecurrentJobCommand>
{
    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Your scheduled task code
    }
}
```

## Cron

To create a task with a cron format schedule, use the `IJobbyClient.ScheduleRecurrentAsync` method (or its synchronous version):

```csharp
// Set a schedule for the task (using cron expression)
// After this, the task will automatically run every 5 minutes
var command = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *");

// Cron expressions with second precision are also supported
// Creating a task that runs every 5 seconds:
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * * *");

// You can optionally pass a RecurrentJobOpts structure
// For example, this creates a scheduled task
// that first runs in one hour, then every 5 minutes
await jobbyClient.ScheduleRecurrentAsync(command, "*/5 * * * *", new RecurrentJobOps
{
    StartTime = DateTime.UtcNow.AddHours(1)
});
```

## Intervals

In addition to the cron format, you can use simple intervals in `TimeSpan` format for scheduling.

For example, this creates a task that runs immediately upon creation and then runs every minute:

```csharp
var command = new RecurrentJobCommand();
var schedule = new TimeSpanSchedule
{
    Interval = TimeSpan.FromMinutes(1)
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

After the task completes, the next execution time will be calculated relative to the completion time. That is, if a task started at 00:00:00 and completed at 00:00:30, it will next run one minute after completion, i.e., at 00:01:30.

This behavior can be changed so that the next execution time is calculated relative to the scheduled _previous_ execution time. Then a task that started at 00:00:00 and completed at 00:00:30 will next run not one minute after completion, but one minute after the previous start, specifically at 00:01:00. The `CalculateNextFromPrev` flag in the schedule object controls this:

```csharp
var command = new RecurrentJobCommand();
var schedule = new TimeSpanSchedule
{
    Interval = TimeSpan.FromMinutes(1),

    // The next execution time
    // will be calculated relative to the previous start time
    // rather than completion time
    CalculcateNextFromPrev = true
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

## Custom Scheduler

In cases where cron or intervals don't provide enough functionality, Jobby allows you to implement custom logic for determining scheduled task execution times with arbitrary schedule formats and parameter sets.

To do this, define a class for schedule parameter objects that implements the marker interface `ISchedule`:

```csharp
class MySpecialSchedule : ISchedule
{
    // You can place any parameters in the class fields
    // that define your schedule
    public string CustomSchedule { get; init; }
}
```

Then, for your schedule, you need to implement a handler class that inherits from the abstract class `BaseScheduleHandler<TSchedule>` and implements three methods:

- **GetSchedulerTypeName** - Returns the name identifying your scheduler type
- **GetFirstStartTime** - Returns the UTC time for the first task execution (called when creating the task)
- **GetNextStartTime** - Returns the UTC time for the next task execution (called after each task execution)

```csharp
class MySpecialScheduleHandler : BaseScheduleHandler<MySpecialSchedule>
{
    // Scheduler name
    public override string GetSchedulerTypeName() => "MY_SPECIAL_SCHEDULE";

    // Time for first execution
    public override DateTime GetFirstStartTime(TimeSpanSchedule schedule, DateTime utcNow)
    {
        // schedule - object with your schedule parameters
        // utcNow - current UTC time
    }

    public override DateTime GetNextStartTime(TimeSpanSchedule schedule, ScheduleCalculationContext ctx)
    {
        // schedule - object with your schedule parameters
        // ctx - contains current time and the time the previous execution was scheduled
    }    
}
```

Then the scheduler needs to be registered during library configuration:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder
        .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UsePostgresql(sp.GetRequiredService<NpgsqlDataSource>())
            
            // Register custom scheduler
            .UseScheduler(new MySpecialScheduleHandler());
    });
});
```

Now you can create scheduled tasks with your custom execution time calculation logic:

```csharp
var command = new RecurrentJobCommand();
var schedule = new MySpecialSchedule
{
    // ...
    // ...
};
await jobbyClient.ScheduleRecurrentAsync(command, schedule);
```

## Exclusivity

By default in Jobby, scheduled tasks are unique based on the `GetJobName()` value of your command. That is, for one `JobName`, you can create **only one** scheduled task. If a scheduled task already exists, calling the `ScheduleRecurrent` method will simply update its schedule.

Example:

```csharp
// Create the first scheduled task for RecurrentJobCommand
var command1 = new RecurrentJobCommand();

// A task will be created that runs every 3 minutes
await jobbyClient.ScheduleRecurrentAsync(command1, "*/3 * * * *");

// Create another task of the same type
var command2 = new RecurrentJobCommand();
await jobbyClient.ScheduleRecurrentAsync(command2, "*/5 * * * *");

// The second task will NOT be created!!!!
// Because a recurrent task with the same JobName already exists
// Instead, the first task's schedule will be updated
```

However, if necessary, uniqueness checking can be disabled via the `IsExclusive`=`false` flag in the `RecurrentJobOpts` structure. The example below shows how to create two identical scheduled tasks, the first running every 3 seconds, and the second running every 5 seconds.

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