# Versions

## v1.1.0 (unreleased)

- Tracing context capture for enqueued jobs (opt-in): a job can capture the tracing context of the `Activity` current at creation time and reuse it at execution. The behaviour is selected with the new `JobbyTracingMode` enum:
  - `CreateNewTraceId` (default) — every execution starts a new trace, previous behaviour
  - `RestoreTraceId` — the full W3C traceparent (TraceId, SpanId and sampling flags) captured at creation becomes the parent context of the execution activity, so the job span is a child of the span that enqueued it; the sampling decision of the original trace is preserved (a job created in an unsampled request is not recorded either)
  - `LinkTraceId` — the execution starts an independent trace referencing the captured one via an `ActivityLink`
  - Set globally via `UseTracing(new JobbyTracingSettings { TracingMode = ... })`, or per job via `JobOpts.TracingMode` (nullable enum, also available through `IHasDefaultJobOptions.GetOptionsForEnqueuedJob`); the per-job value overrides the global setting. The modes take effect only when tracing is enabled with `UseTracing`; call it before `CreateJobsFactory`/`CreateJobbyClient` — settings are applied to a factory at its creation. The resolved mode is persisted in the new `tracing_mode` column of `jobby_jobs` (alongside the new `traceparent` column), so a job's tracing behaviour is pinned at creation time
  - Recurrent jobs always start a new trace on each run, tracing settings have no effect on them; a non-W3C (hierarchical) ambient Activity is not captured (execution starts a new trace)
  - Potentially breaking changes:
    - `IJobbyComponentsConfigurable.UseTracing()` now takes an optional `JobbyTracingSettings?` parameter — external implementations of the interface must be updated to the new signature
    - Job execution spans are now named after the job name (e.g. `DemoJob`) instead of the fixed `JobExecution`, and the `job_name` span tag is removed — the job name is carried by the span name itself. Queries, dashboards or alerts filtering on `name = "JobExecution"` or on the `job_name` tag must be updated to filter on the span name

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