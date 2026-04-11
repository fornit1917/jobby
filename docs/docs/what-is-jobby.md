# What is Jobby?

**Jobby** is a high-performance background job management library for .NET. Built for those who need maximum efficiency, strong data consistency, and rich functionality without compromises, Jobby combines best practices from the worlds of queues, schedulers, and CQRS patterns into an intuitive and extensible API.

If you're looking for a faster, more resource-efficient alternative to Hangfire or Quartz — Jobby is your solution.

## Key Features

Jobby offers a comprehensive set of features for background job processing:

- **Commands and Handlers (CQRS approach)**. Inspired by the MediatR pattern, Jobby's API lets you define jobs as simple commands (POCO objects) and process them in dedicated handler classes, keeping your code clean and testable.
- **Flexible Execution Scenarios**.
    - **Fire-and-forget jobs**: Enqueue jobs for immediate background processing.
    - **Delayed jobs**: Schedule jobs to execute after a specified delay.
    - **Recurring jobs**: Configure periodic execution using CRON expressions, simple intervals, or implement custom scheduling logic by extending the provided interfaces.
- **Transactional Consistency**. A key strength of Jobby is its ability to create multiple jobs transactionally, including within your application's business transaction. Thanks to seamless integration with Entity Framework and other ORMs, you can easily implement the Transactional Outbox pattern, guaranteeing that jobs are enqueued only if all related data operations succeed.
- **Execution Order Control**. Jobby gives you full control over processing sequences:
    - **Ordered batches**: When creating multiple jobs at once, you can explicitly specify their execution order.
    - **Serializable Groups (Partitioning)**: Group jobs by a key (e.g., `OrderId`). All jobs within the same group are guaranteed to execute sequentially — ideal for processing events related to a single business process (similar to Kafka partitions).
- **Reliability and Fault Tolerance**.
    - **Retry policies**: Configure flexible rules for automatic retries of failed jobs with exponential backoff and other strategies.
    - **Resilience to failures**: Designed for distributed systems, Jobby ensures job execution even during temporary unavailability of individual components or the database.
- **Extensibility and Scalability**.
    - **Scalability**: Jobby works correctly in multi-instance (distributed) deployments.
    - **Multiple queues**: Distribute jobs across different queues to prevent critical operations from being blocked by less important ones.
    - **Middleware pipeline**: Inject custom logic into the job execution pipeline — for logging, performance measurement, distributed locking, and more.

## Why Jobby?

Jobby was built with modern development realities in mind, where both feature richness and resource efficiency matter.

- **High Performance**. Benchmarks show Jobby is **multiple times faster** than Hangfire and Quartz. This is achieved through a minimalist database schema, combining multiple operations into single SQL queries, and efficiently leveraging modern async/await and multi-threading capabilities.
- **Low Resource Consumption**. Minimal impact on your .NET application's CPU and RAM, plus a gentle, optimized load on the database. Jobby avoids unnecessary connections and wasteful polling, allowing your application to handle more user traffic.
- **Modern Observability (OpenTelemetry)**. Built-in metrics and tracing support, providing full compatibility with your existing observability stack (Prometheus, Jaeger, Grafana, etc.) out of the box.
- **Simple Integration**. Built for .NET 8+ and PostgreSQL (with a pluggable architecture for other storage providers), Jobby integrates seamlessly with ASP.NET Core and the standard DI container — no complex configuration or hacks required.

## Summary

**Jobby** is the choice for teams that value:
- **Data consistency** above all else.
- **Rich functionality** covering both simple and advanced scenarios (groups, ordering, outbox).
- **Extensibility** and the ability to adapt the library to specific needs.
- **Scalability** from a single monolith to a distributed cluster.
- **Reliability** and confidence that jobs won't be lost due to failures.
- **High performance** and efficient use of server resources.

Proceed to the [Quick Start](./quickstart) guide to see Jobby in action!