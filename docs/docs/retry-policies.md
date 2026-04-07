# Retry Policies

If an error occurs while executing a background task, Jobby can run it again. If the task is recurrent, it will be run again according to its schedule. If the task is non-recurrent, it will be run according to the configured _retry policy_.

## Basic Configuration

A retry policy is a `RetryPolicy` object that contains:
- The maximum number of attempts to execute a task
- The pause values in seconds between retry attempts

```csharp
var retryPolicy = new RetryPolicy
{
    // Maximum total number of task execution attempts
    // A value of 3 means that if the task fails
    // on the first attempt,
    // two additional retry attempts will be made
    MaxCount = 3,

    // Pauses between execution attempts when tasks fail
    // First retry will occur after one second,
    // second retry after two seconds
    IntervalsSeconds = [1, 2]
}

// You don't need to specify all values in IntervalSeconds
// For example, if we want to retry a task 10 times 
// every 10 minutes, the following approach works:
retryPolicy = new RetryPolicy
{
    MaxCount = 11,
    IntervalsSeconds = [600]
}
```

The retry policy can be global (applied to all tasks) or unique to a specific task type. This is configured during library setup:

```csharp
jobbyBuilder
    // the policy from the defaultPolicy object will be applied by default
    .UseDefaultRetryPolicy(defaultPolicy)
    // but a different policy will be applied for SendEmailCommand tasks
    .UseRetryPolicyForJob<SendEmailCommand>(specialRetryPolicy);
```

## Jitter

Jobby can add an additional random delay (so-called Jitter) to the fixed pause values between attempts.

To enable this feature, you need to populate the `JitterMaxValuesMs` list in the `RetryPolicy` object, which contains the maximum additional delay values in milliseconds for pauses between specific attempts.

The example below shows a retry policy that, after the first attempt, adds a pause of 1 second plus a random value up to 100 ms, and after the second attempt, 2 seconds plus a random value up to 200 ms.

```csharp
retryPolicy = new RetryPolicy
{
    MaxCount = 3,
    IntervalsSeconds = [1, 2],

    // Values for additional random delays
    // in milliseconds
    JitterMaxValuesMs = [100, 200]
}
```