# Multi-queues

By default, all Jobby tasks (including scheduled tasks) are fetched for execution from a single queue. With a large number of different task types and high creation intensity, this approach can become inefficient.

As an alternative, Jobby allows you to define multiple named queues with different processing settings and distribute tasks among these queues.

## The Single Queue Problem

What specific problems can arise from using a single queue?

Imagine you have an important scheduled task and tasks that are executed for one-time processing.

Suppose the scheduled task needs to run at 00:00:00. But at 23:59:55, a huge number of one-time tasks start entering the system, scheduled to run between 23:59:55 and 00:00:00.

Since the Jobby server executes tasks in increasing order of their scheduled start time, it will not run the scheduled task at 00:00:00 if it hasn't yet executed all tasks scheduled between 23:59:55 and 00:00:00. And if there are enough of those tasks, the scheduled task will be executed only after a very significant delay.

In other words: **a large number of tasks of one type can prevent timely execution of tasks of another type**.

Another problem with a single queue: tasks of all types are executed with the same concurrency limits. For example, for most task types you might allow up to 10 concurrent tasks, so you set `MaxDegreeOfParallelism` to `10`. But at the same time, you have a task type that heavily loads the database, and **running 10 such tasks simultaneously could lead to excessive load and even failure**. You would like to run no more than one such task at a time.

The **multi-queue** mechanism solves all these problems in the following ways:

- For each Jobby server, a set of named queues that it serves is defined
- Each queue can have its own concurrency limit
- All created tasks can be distributed across different queues. A queue can be specified either at the command class level for all tasks of a specific type, or when creating each individual task instance
- Each Jobby server regularly switches between queues, so a large number of tasks in one queue will not block timely execution of tasks from another queue

## Server Configuration

By default, all tasks are created in a queue named `default`, and each Jobby server only serves this default queue.

To use multiple queues, you need to specify all of them (including `default`) in the `JobbyServerSettings.Queues` parameter. Each queue in this list has the following available settings:

- **QueueName** - The name of the queue. Required parameter.
- **MaxDegreeOfParallelism** - The maximum degree of parallelism when executing tasks from this queue. Optional parameter, defaults to `JobbyServerSettings.MaxDegreeOfParallelism`. Can be overridden to a lower value.
- **DisableSerializableGroups** - When set to `false`, this flag disables (for optimization purposes) the sequential execution mechanism within groups for this queue (see [Sequential Execution](./sequential-execution) for details). Default value is `true`. **Important:** when using the value `false`, the library will only work correctly if all tasks in the queue have `SerializableGroupId` = `null`.

Example configuration:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseServerSettings(new JobbyServerSettings
            {
                MaxDegreeOfParallelism = 10,
                Queues = [
                    // default queue
                    new QueueSettings 
                    {
                        QueueName = "default"
                    },

                    // queue for tasks
                    // that need to run on time
                    // for example, recurring tasks
                    new QueueSettings
                    {
                        QueueName = "important"
                    },

                    // queue for heavy tasks
                    // for which we want to reduce
                    // the degree of parallelism
                    new QueueSettings
                    {
                        QueueName = "heavy",
                        MaxDegreeOfParallelism = 2
                    },                    
                ]
            });
    });
});
```

**Important**: if you do not specify a queue in the server settings, tasks created in that queue will not be executed.

## Choosing a Queue for a Task

By default, all tasks go to the `default` queue. There are several ways to override the queue.

First, when configuring the library, you can specify the queue where all _scheduled tasks_ will go by default:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{
    // All recurring tasks by default
    // will go to the important queue
    // instead of the default queue
    jobbyBuilder.UseQueueForAllRecurrent("important");
        
    // ...
});
```

Second, you can specify the default queue in the `JobOpts` and `RecurrentJobOpts` structures at the command class level by implementing `IHasDefaultJobOptions`:

```csharp
class MyHeavyCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "MyHeavyJob";

    // All regular tasks for MyHeavyCommand
    // will be created in the heavy queue
    public JobOpts GetOptionsForEnqueuedJob()
    {
        return new JobOpts
        {
            QueueName = "heavy"
        }
    }

    // All scheduled tasks for MyHeavyCommand
    // will be created in the heavy queue
    public RecurrentJobOpts GetOptionsForRecurrentJob()
    {
        return new RecurrentJobOpts()
        {
            QueueName = "heavy"
        }
    }
}
```

And third, the queue for a task can be specified directly when creating its instance:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    QueueName = "heavy"
});

// similarly for scheduled tasks
// and for jobbyClient.Factory methods
```

## Recommended Configuration

As we saw in the example above, using a single queue can lead to undesirable consequences. But creating too many queues can also negatively impact performance and increase database load.

Therefore, it's recommended to use a small number of queues, for example:

- A queue for tasks that need to run on time (for example, scheduled tasks)
- A queue for heavy tasks for which you want to reduce the degree of parallelism
- A queue for tasks that don't need (or conversely do need) support for sequential execution groups (see [Sequential Execution](./sequential-execution))
- The default queue