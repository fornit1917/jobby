# Fault Tolerance

Jobby can, in many cases, survive the failure of certain components and continue working correctly without data loss or manual intervention.

## Database Failure

If the database becomes unavailable, Jobby will retry the following operations that failed with an error:

- Retrieving the next jobs ready to be executed
- Writing updated statuses of completed or failed jobs to the database

When database connectivity is restored, Jobby will complete the operations that previously failed and return to normal operation mode.

Retries for database error operations are performed at an interval specified in the `JobbyServerSettings.DbErrorPauseMs` parameter. The default value is 5000 ms and can be overridden as follows during library configuration:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            // ...
            .UseServerSettings(new JobbyServerSettings
            {
                // Retry database queries that failed with an error
                // every 10 seconds
                DbErrorPauseMs = 10000
            });
    });
});
```

## Jobby Server Failure

### Restarting Jobs from Failed Instances

If the Jobby server is running on multiple instances and one of them fails, jobs will continue to execute on the remaining instances.

But what happens to jobs that were started but not completed before the instance they were running on went down?

Jobby implements a Heartbeat mechanism for automatically detecting failed nodes. Jobs that were running on failed nodes will be automatically restarted on live instances.

If your background job is not idempotent and its automatic restart could lead to undesirable consequences, you can disable its restart in such situations via the `JobOpts.CanBeRestartedIfServerGoesDown` flag (the same-named flag is also available for scheduled jobs in the `RecurrentJobOpts` structure). If this flag is set to `false`, the job that was running on a failed node will not be automatically restarted.

You can override this flag's value when creating a specific background job instance:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    CanBeRestartedIfServerGoesDown = false
});
```

Or you can override it by default for all jobs of a specific type at the command class level by implementing `IHasDefaultJobOptions`:

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

### Heartbeat Settings

The Heartbeat mechanism has several settings in `JobbyServerSettings`:

- **HeartbeatIntervalSeconds** - the interval in seconds at which each Jobby server sends a heartbeat signal indicating it is operational. The default value is **10 seconds**.
- **MaxNoHeartbeatIntervalSeconds** - the maximum number of seconds after which a server that has not sent any heartbeat signal is considered to have failed. The default value is **300 seconds**.

These parameters can be overridden if necessary during library configuration:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            // ...
            .UseServerSettings(new JobbyServerSettings
            {
                // Send heartbeat signals
                // every 5 seconds
                HeartbeatIntervalSeconds = 5,

                // If no signal has been received
                // from the server for 60 seconds
                // consider it non-operational
                MaxNoHeartbeatIntervalSeconds = 60,
            });
    });
});
```