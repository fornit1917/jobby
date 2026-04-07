---
# https://vitepress.dev/reference/default-theme-home-page
layout: home

hero:
  name: "Jobby"
  text: "High-performance and reliable .NET library for background jobs"
  tagline: Opensource and free project with MIT-license
  actions:
    - theme: alt
      text: What is Jobby
      link: /docs/what-is-jobby
    - theme: brand
      text: Quickstart
      link: /docs/quickstart

features:
  - title: Consistency
    details: Background tasks are stored in the database. Transactional creation of multiple tasks and creation of tasks in the same transaction as your business operations are supported.
  - title: Functionality
    details: Tasks multi-queue, scheduled tasks, configurable execution order, flexible retry policies for failed tasks, OpenTelemetry-compatible metrics and tracing and many other features are available out of the box.
  - title: Extensibility
    details: Easy integration with ORM, the ability to use custom schedulers, a customizable middlewares-pipeline for task execution.
  - title: Scalability
    details: Supports parallel execution of tasks both within a single instance and across multiple instances in distributed systems.
  - title: Reliability
    details: Automatically detects dead instances and distributes their tasks among living ones. In the event of a database failure, it gracefully waits for it to recover and retry operations without data loss.
  - title: High-performance
    details: It works several times faster than similar libraries (such as Hangfire or Quartz) and consumes several times fewer resources both on the .NET side and on the DB side.
---

