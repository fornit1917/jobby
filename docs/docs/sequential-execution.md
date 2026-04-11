# Sequential Execution

Jobby provides two ways to define a set of tasks that will be executed strictly sequentially:

- Creating a task chain using `JobsSequenceBuilder`
- Specifying a `SerializableGroupId` parameter for tasks to group them, ensuring tasks within each group execute strictly sequentially

## JobsSequenceBuilder

The first approach is applicable when you need to create several tasks for sequential execution at once within a single transaction.

```csharp
var sequenceBuilder = jobbyClient.Factory.CreateSequenceBuilder();

// Tasks will be executed in the exact order specified below
sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "first@email.com" }));

sequenceBuilder.Add(jobbyClient.Factory
    .Create(new SendEmailCommand { Email = "second@email.com" }));

var jobs = sequenceBuilder.GetJobs();

await jobbyClient.EnqueueBatchAsync(jobs);
```

Tasks created via JobsSequenceBuilder are guaranteed to be executed in the specified order, even if they were created in different queues.

If one of the tasks fails, subsequent tasks will not be started.

## Sequential Execution Groups

Jobby allows you to specify a group identifier in the `SerializableGroupId` parameter when creating a task. Jobby will execute tasks with the same identifier strictly one at a time, in ascending order of their scheduled start time. Jobby guarantees that at most one task from each group will execute simultaneously.

Unlike the first approach, this method can be applied even when tasks within the same group are created at different times and in different transactions.

This approach guarantees that tasks within the same group will be executed strictly one at a time, but the order of their execution by ascending scheduled start time is guaranteed only if all tasks in the group are created in the same queue.

### Group Identifier for a Task

The `SerializableGroupId` parameter can be defined at the command level by implementing the `IHasDefaultJobOptions` interface:

```csharp
class UpdateOrderCommand : IJobCommand, IHasDefaultJobOptions
{
    public static string GetJobName() => "UpdateOrderJob";

    public required string OrderId { get; init; }

    // Tasks for the same OrderId
    // will execute sequentially
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

Or `SerializableGroupId` can be specified directly when creating a specific task instance:

```csharp
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    SerializableGroupId = command.OrderId
});
```

Similarly, the group identifier can be defined for scheduled tasks as well.

### Disabling Sequential Execution Groups Functionality

If you do not plan to populate `SerializableGroupId` in your tasks, you can disable this functionality to improve library performance (though this is not required):

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseServerSettings(new JobbyServerSettings
            {
                // Disable sequential execution groups
                DisableSerializableGroups = true
            });
    });
});
```

The `DisableSerializableGroups` flag can also be set at the individual queue level. For example, you can disable sequential execution groups globally and enable them only for a single queue where you plan to create tasks with `SerializableGroupId` populated:

```csharp
builder.Services.AddJobbyServerAndClient(jobbyBuilder =>
{    
    jobbyBuilder.ConfigureJobby((sp, jobby) =>
    {
        jobby
            .UseServerSettings(new JobbyServerSettings
            {
                // Disable sequential execution groups globally
                DisableSerializableGroups = true,
                Queues = [
                    new()
                    {
                        QueueName = "for_groups",

                        // But enable for a specific queue
                        DisableSerializableGroups = false,
                    }
                ]
            });
    });
});
```

Manipulating the `DisableSerializableGroups` flag is not required but will help achieve a more optimal operating mode for Jobby if you plan to use `SerializableGroupId` for only some tasks.

### Group Locking on Error

By default, a group is not locked if one of its tasks fails. After all retry attempts fail, the task will transition to the `Failed` status, and the next task in the group will be executed.

This behavior can be changed for non-recurring tasks: if all retry attempts fail, the group will remain locked, and no other tasks from the group will be executed. To enable this, set the `JobOpts.LockGroupIfFailed` parameter to `true`:

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

            // If the task fails
            // other tasks from the group will not run
            LockGroupIfFailed = true,
        }
    }

    public RecurrentJobOpts GetOptionsForRecurrentJob()
    {
        return default;
    }
}

// Or directly when creating a task instance
await jobbyClient.EnqueueCommandAsync(command, new JobOpts
{
    SerializableGroupId = command.OrderId,
    LockGroupIfFailed = true,
});
```

This feature should be used cautiously because if there are many ready-to-execute tasks in groups that are locked for extended periods, task execution performance will decrease and database load will increase.

### Freezing and Unfreezing Locked Groups

Jobby has a built-in capability to automatically detect groups locked by failed tasks. If Jobby detects a large number of ready-to-execute tasks in such locked groups, it will transition them to a `Frozen` status to prevent negative impact on task execution performance.

A UI will be added in future versions where you can view the list of locked groups and, if necessary, initiate the unfreezing and unlocking process.

Until the UI is available, these operations can be performed via SQL queries if needed.

```sql
-- Get IDs of groups locked by failed tasks
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

-- Initiate unlocking and unfreezing for a group
INSERT INTO jobby_unlocking_groups (group_id, created_id)
VALUES (<group id to unlock>, now());
```