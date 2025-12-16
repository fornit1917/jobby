# Versions

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