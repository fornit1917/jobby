# Jobs Definition

## Command and Handler

To define a background job, you need to implement the `IJobCommand` interface for the job parameter object and the `IJobCommandHandler` interface for the job execution code:

```csharp
public class SendEmailCommand : IJobCommand
{
    // Properties can contain any parameters
    // that will be passed to the job
    public string Email { get; init; }

    // This method must return a unique name
    // identifying the job type
    public static string GetJobName() => "SendEmail";
}

public class SendEmailCommandHandler : IJobCommandHandler<SendEmailCommand>
{
    // When using the Jobby.AspNetCore package,
    // dependency injection from the DI container is supported
    private readonly IEmailService _emailService;
    public SendEmailCommandHandler(IEmailService logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(SendEmailCommand command, JobExecutionContext ctx)
    {
        // Here you can write the code that executes your job
        // command - job parameters
        // ctx - contains CancellationToken and additional information
    }
}
```

## Adding Jobs to Jobby

When configuring the library, you must specify the assemblies containing your command and handler code:

```csharp
builder.Services.AddJobbyServerAndClient((IAspNetCoreJobbyConfigurable jobbyBuilder) =>
{
    // Specify assemblies containing commands and handlers
    jobbyBuilder
        .AddJobsFromAssemblies(typeof(DemoJobCommand).Assembly);
    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        // ...
    });
});
```

## JobExecutionContext

In addition to the command object, an instance of the `JobExecutionContext` class is passed to the `ExecuteAsync` method with the following fields:

- **JobName** - The name of the job type (matches the value you specified in the `TCommand.GetJobName()` method).
- **StartedCount** - The current attempt number for executing this background job.
- **IsLastAttempt** - A flag indicating whether the current attempt is the last one according to the configured retry policy. If `true`, the job will not be restarted again upon failure.
- **IsRecurrent** - A flag indicating that the current job is a scheduled job.
- **CancellationToken** - A CancellationToken that transitions to a canceled state when the server running the job is stopped.

## Default Options for Jobs

In the command class, you can optionally implement the `IHasDefaultJobOptions` interface with the following methods:

- **GetOptionsForEnqueuedJob()** - Returns background job settings to be used by default when running a command *through the queue*.
- **GetOptionsForRecurrentJob()** - Returns background job settings to be used by default when running a command as a *scheduled job*.

Example for specifying queue names where background jobs will be created for all commands of this type:

```csharp
public class SomeCommand : IJobCommand, IHasDefaultJobOptions
{
    public string SomeParam { get; init; }
    public static string GetJobName() => "SomeJob";

    // Specify the queue name where
    // all background jobs for SomeCommand will be created
    public JobOpts GetOptionsForEnqueuedJob() => new JobOpts 
    {
        QueueName = "SomeQueue"
    };

    // Specify the queue name where
    // all scheduled background jobs for SomeCommand will be created
    public RecurrentJobOpts GetOptionsForRecurrentJob() => new RecurrentJobOpts
    {
        QueueName = "SomeRecurrentJobsQueue"
    };
}
```

These methods return the `JobOpts` and `RecurrentJobOpts` structures respectively.

In the `JobOpts` structure, you can override the following parameters for one-time jobs executed via the queue:

- **StartTime** - The time before which the job must not be started. See [Jobs Running](./jobs-enqueue) for details.
- **QueueName** - The queue name for the created job. See [Multi-Queues](./multiqueues) for details.
- **SerializableGroupId** - The group identifier for strictly sequential execution of jobs within the group. Default is `null`. See [Sequential Execution](./sequential-execution) for details.
- **LockGroupIfFailed** - A flag indicating that the group specified in `SerializableGroupId` should remain locked if the job fails to execute. Default is `false`. See [Sequential Execution](./sequential-execution) for details.
- **CanBeRestartedIfServerGoesDown** - A flag indicating whether the job may be restarted if the server executing it is presumed to have stopped functioning. Default is `true`. See [Fault Tolerance](./fault-tolerance) for details.

In the `RecurrentJobOpts` structure, you can override the following parameters for scheduled jobs:

- **StartTime** - The time before which the scheduled job must not be started for the first time. See [Scheduled Jobs](./jobs-schedule) for details.
- **QueueName** - The queue name for the created scheduled job. See [Multi-Queues](./multiqueues) for details.
- **SerializableGroupId** - The group identifier for strictly sequential execution of jobs within the group. Default is `null`. See [Sequential Execution](./sequential-execution) for details.
- **CanBeRestartedIfServerGoesDown** - A flag indicating whether the job may continue to run according to its schedule if the server executing it is presumed to have stopped functioning. Default is `true`. See [Fault Tolerance](./fault-tolerance) for details.
- **IsExclusive** - A flag indicating whether more than one scheduled job may be created for the `JobName` specified in the command. Default is `false`. See [Scheduled Jobs](./jobs-schedule) for details.