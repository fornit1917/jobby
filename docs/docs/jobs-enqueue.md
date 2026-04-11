# Running Background Jobs via Queue

Jobby allows you to send commands to the queue for one-time execution of their handlers in the background.

## Running via IJobbyClient

To add jobs to the queue, you can use the `IJobbyClient` service. When using Jobby.AspNetCore, an instance of this service can be obtained from the DI container, or from the `JobbyBuilder` object otherwise (see [Installation and Configuration](./install-and-config) for details).

### Running a Single Job

```csharp
var command = new SendEmailCommand 
{ 
    Email = "some@email.com"
};

// Add a job to the queue for execution as soon as possible
await jobbyClient.EnqueueCommandAsync(command); 

// Add a job to the queue for execution no earlier than the specified time
await jobbyClient.EnqueueCommandAsync(command, DateTime.UtcNow.AddHours(1));

// Alternatively, you can use synchronous versions of the methods
jobbyClient.EnqueueCommand(command);
```

The above methods optionally accept a `JobOpts` structure — additional settings with which the background job will be created. The parameters included in this structure are described on the [Job Definition](./jobs-definition) page.

`JobOpts` parameters passed to the `EnqueueCommand` method have higher priority than parameters defined at the command class level when implementing the `IHasDefaultJobOptions` interface. Let's look at an example of how parameters defined at different levels are merged:

```csharp
class SomeCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "SomeJob";

    // Options at the command class level
    public JobOpts GetOptionsForEnqueuedJob() => new JobOpts
    {
        QueueName = "queue_1",
        CanBeRestartedIfServerGoesDown = false
    }
}

// Adding a job to the queue
// with queue name override
var command = new SomeCommand();
var opts = new JobOpts
{
    QueueName = "queue_2",
}

await jobbyClient.EnqueueCommandAsync(command, opts); 
```

The resulting background job will be created with the following options:

- `QueueName` = `"queue_2"` because this parameter was overridden in the `Enqueue` method
- `CanBeRestartedIfServerGoesDown` = `false` because this parameter is defined at the command level and was not overridden in the `Enqueue` method

### Running Multiple Jobs

To add multiple jobs within a single transaction, prepare a list of `JobCreationModel` objects using the factory available at `jobbyClient.Factory`, then call the `EnqueueBatchAsync` method:

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

The `Create` method, similarly to the `Enqueue` method, can accept a `JobOpts` parameter.

When adding multiple jobs, there is an option to enforce a strict execution order:

```csharp
var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();

// Jobs will be executed in the exact order specified below
sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "first@email.com" }));

sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "second@email.com" }));

var jobs = sequenceBuilder.GetJobs();

await jobbyClient.EnqueueBatchAsync(jobs);
```

### Job Groups with Sequential Execution

When creating a job, you can specify a group identifier, and Jobby will guarantee that no more than one job is executed at any given time within each group.

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts 
{
    SerializableGroupId = "SomeGroupId"
});
```

The next job in the group will start only after the current job completes, whether successfully or unsuccessfully. If you need to block the execution of any jobs from the same group upon unsuccessful completion, set the `LockGroupIfFailed` flag when creating the job:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts 
{
    SerializableGroupId = "SomeGroupId",
    LockGroupIfFailed = true
});
```

For more details about this feature, see [Sequential Execution](./sequential-execution).

## Running via ORM

If your project uses EntityFramework (or another ORM), you can use your DbContext to add jobs. This can be useful when you need to both queue a job and perform an operation on a domain entity within the same transaction.

To do this:

- Add the `JobCreationModel` entity to your DbContext
- Configure the table name as `jobby_jobs` for this entity and specify the `Id` field as the primary key
- Apply the appropriate naming convention (snake_case for PostgreSQL)

Now you can create background jobs as follows:
- Create `JobCreationModel` objects using `jobbyClient.Factory`
- Save them to the database using standard DbContext methods

```csharp
public class YourDbContext : DbContext
{
    // Add a DbSet for the JobCreationModel entity
    public DbSet<JobCreationModel> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<JobCreationModel>()
            .ToTable("jobby_jobs");

        modelBuilder
            .Entity<JobCreationModel>()
            .HasKey(x => x.Id);

        // Then apply the snake_case naming convention
        // ...
    }
}

// Queuing a job via EF
var command = new SendEmailCommand { Email = "some@email.com" };
var jobEntity = jobbyClient.Factory.Create(command);
_dbContext.Jobs.Add(job);
await _dbContext.SaveChangesAsync();
```

An example of using EF is available here: [Jobby.Samples.AspNet](https://github.com/fornit1917/jobby/tree/master/samples/Jobby.Samples.AspNet).

## Job Cancellation

A task added to the queue can be cancelled if it hasn't been started yet.

```csharp
var startTime = DateTime.UtcNow.AddHours(1);

// Create a task
// and get its instance id
var jobId = await jobbyClient.EnqueueCommandAsync(command, startTime);

// Cancel the task by instance id
await jobbyClient.CancelJobsByIdsAsync(jobId);
```