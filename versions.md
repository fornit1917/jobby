# Versions

## Unreleased

- Added the optional ASP.NET Core Dashboard with an embedded Blazor WebAssembly UI, polling JSON API, and PostgreSQL storage. It provides job and queue views, scheduled-date filtering, recurring definitions, server health, and job and locked-group management. Authorization supports host or inline policies, explicit anonymous access, and the optional `Jobby.Dashboard.Authorization.Basic` package, with separate read and management permissions, server-enforced read-only mode, and antiforgery protection.
  - Job lists are read through the existing `(queue_name, status, scheduled_start_at)` index, and each application instance shares one per-queue/status count query between open dashboards. `PostgresqlDashboardStorageOptions.CountsCacheDuration` (2 seconds by default) controls how long those counts are reused

## v1.0.5 (2026-11-01)

- Fixed false `InvalidJobsConfigException: Command X does not have handler` in `AddJobsFromAssemblies` when a single handler class implements `IJobCommandHandler<>` for several commands
  - Previously only an arbitrary one of the implemented handler interfaces was detected (interface order is not guaranteed), so the rest of the commands looked handler-less. Now all commands handled by the same class are registered
- `AddJobsFromAssemblies` now supports commands that implement `GetJobName` explicitly (`static string IJobCommand.GetJobName()`)
  - Previously such commands were silently skipped during scanning: jobs were created successfully, but every execution attempt failed with `Job X does not have suitable handler`
- Invalid schedules now fail job creation. `ScheduleRecurrent` / `CreateRecurrent` throw `InvalidScheduleException` immediately if the cron expression cannot be parsed or the interval is not positive
  - Previously these errors surfaced only after the first execution, and the job was silently rescheduled to run every minute instead of the specified schedule; a zero interval made the job run on every polling iteration endlessly
  - Note: a zero interval is no longer allowed, intervals must be strictly positive
- New optional `EnsureScheduleIsValid` method for custom schedulers (`IScheduleHandler<TSchedule>`, `BaseScheduleHandler<TSchedule>`): it is called during job creation, override it to reject an invalid custom schedule at creation time instead of runtime
- Failed job executions are now distinguishable in traces: tracing middleware sets `ActivityStatusCode.Error` with the exception message and adds an `exception` event containing the exception type, message and stack trace
  - Previously failed executions looked identical to successful ones
- Multiple `ConfigureJobby(Action<IServiceProvider, IJobbyComponentsConfigurable>)` calls are now all applied in registration order
  - Previously each call overwrote the previous one, so only the last configuration took effect
- `JobbyBuilder.CreateJobbyServer` can now be called repeatedly on the same builder: metrics/tracing middlewares are no longer duplicated in the execution pipeline after each call
- Freezing of permanent-locked groups now transitions only jobs in `Scheduled` status to `Frozen`
  - Previously jobs in other statuses could be frozen too
- Group unlocking now deletes only the group-locker jobs that are failed or stuck (cannot be restarted and their server is no longer alive)
  - Previously all locker jobs of the unlocked group were deleted, which could remove tasks that should keep executing
- Fixed misleading validation error message for `JobbyServerSettings.PermanentLockedHandleUnlockingRequestsIntervalSeconds`: it referenced another property name

## v1.0.4 (2026-10-05)

- Catch OperationCanceledException instead of TaskCanceledException
- Send first heartbeat signal before server running
- Support IAsyncDisposable for scoped user services
- Fix minor bugs

## v1.0.3 (2026-09-20)

- Fixed hanging retries of failed tasks in serializable groups
- Fixed semaphore slot leak when the database is unavailable
- On server shutdown, a task that threw OperationCancelledException is no longer marked as failed and can be picked up by another instance for retry
- Fixed a possible error during graceful server shutdown
- Improved database query performance in the heartbeat mechanism

## v1.0.2 (2026-04-28)

- Do not recalculate the start time when re-creating an existing exclusive recurring job if the schedule has not changed

## v1.0.1 (2026-04-23)

- Removed mistakenly added dependency on Quartz

## v1.0.0 (2026-04-11)

- New methods for jobs configuration
    - Optional `JobOpts` and `RecurrentJobOpts` parameters in job creation methods
    - Optional `IHasDefaultJobOptions` interface for commands
- Method `IJobCommand.CanBeRestarted` removed. Use `JobOpts.CanBeRestartedIfServerGoesDown` and `RecurrentJobOpts.CanBeRestartedIfServerGoesDown` instead
- Multi-Queues
- Sequentional execution via SerializableGroupId
- New opportunities for recurrent jobs
    - Not exclusive recurrent jobs
    - Intervals scheduler
    - Ability to implement custom schedulers
- `JobCreationModel` has been made read-only. Use `IJobsFactory` or `IJobbyClient.Factory` for create objects of `CreationModel`
- Removed deprecated configuration classes and methods
    - JobbyServicesBuilder (use JubbyBuilder instead)
    - IJobbyServicesConfigurable (use IJobbyComponentsConfigurable and IJobbyJobsConfigurable instead)
    - IServiceCollection.AddJobby (use IServiceCollection.AddJobbyServerAndClient instead)

## v0.6.2 (2026-04-10)

- Fixed a bug with stuck recurrent jobs

## v0.6.1 (2026-01-11)

- Fixed a bug where the library would throw an error when using custom table names in the database

## v0.6.0 (2025-01-09)

- Service `IJobyStorageMigrator` for creating and updating database schema
- Using PostgreSQL friendly UUIDv7 for jobs primary keys
- Minor refactoring of integration tests

## v0.5.0 (2025-12-16)

- Configurable middlewares pipeline for jobs execution
- Jobs execution metrics (like numbers of started / completed / failed jobs and job execution time histogram)
- Jobs execution tracing
- Ability to export metrics and traces via OpenTelemetry

## v0.4.0 (2025-11-03)

- Random additional delay in retry policies (Jitter)
- Ability to use IServiceProvider for jobby configuration
- Deprecated (will be removed in v1.0.0):
    - JobbyServicesBuilder (use JubbyBuilder instead)
    - IJobbyServicesConfigurable (use IJobbyComponentsConfigurable and IJobbyJobsConfigurable instead)
    - IServiceCollection.AddJobby (use IServiceCollection.AddJobbyServerAndClient instead)

## v0.3.0 (2025-10-12)

- Calling jobs without reflection
- Increasing polling interval

## v0.2.0 (2025-08-25)

- MIT license
- Fixed possible "split brain" situation after restart stuck jobs from temporary unavailable nodes

## v0.1.0 (2025-08-03)

- Scheduled tasks
- Queue-based task execution
- Transactional creation of multiple tasks
- Configurable execution order for multiple tasks
- Retry policies for failed tasks
- Proper operation in distributed applications
- Fault tolerance and component failure resilience