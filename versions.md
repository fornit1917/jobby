# Versions

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