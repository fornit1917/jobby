# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Code Navigation Preferences

When searching for code references, definitions, or usages:
- **Always prefer LSP tools** (findReferences, goToDefinition, documentSymbol, hover) over grep/ripgrep for C# files
- Use `LSP findReferences` to find all usages of symbols
- Use `LSP goToDefinition` to navigate to definitions
- Use `LSP documentSymbol` to list symbols in a file
- Only fall back to grep when LSP is unavailable or for non-code searches (logs, configs, etc.)

## Build Commands

```bash
# Restore and build
dotnet restore
dotnet build

# Build specific project
dotnet build src/Jobby.Core
dotnet build src/Jobby.Postgres
dotnet build src/Jobby.AspNetCore
```

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Jobby.Tests.Core
dotnet test tests/Jobby.Tests.Postgres
dotnet test tests/Jobby.IntegrationTests.Postgres

# Run single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"
dotnet test --filter "ClassName.TestMethodName"

# Run with verbose output
dotnet test --verbosity normal
```

**Integration tests require PostgreSQL**: Start with `docker-compose -f docker-compose.postgresql.yml up -d` before running `Jobby.IntegrationTests.Postgres`.

## Project Overview

Jobby is a distributed background task execution library for .NET 8. It provides scheduled tasks, queue-based execution, retry policies, and OpenTelemetry observability.

## Architecture

### Core Projects (src/)

- **Jobby.Core** - Core abstractions and services. Contains `IJobCommand`/`IJobCommandHandler` interfaces, `JobbyBuilder`, `JobbyClient`, `JobbyServer`, retry policies, and middleware pipeline.
- **Jobby.Postgres** - PostgreSQL storage implementation via `IJobbyStorage`. Uses Npgsql directly (not EF Core). Schema migrations in `Migrations/` folder using Evolve.
- **Jobby.AspNetCore** - ASP.NET Core integration. Provides `AddJobbyServerAndClient()` extension method and `JobbyHostedService` for lifecycle management.

### Key Patterns

**Job Definition**: Commands implement `IJobCommand` (with static `GetJobName()` and `CanBeRestarted()`), handlers implement `IJobCommandHandler<TCommand>`.

**Builder Pattern**: `JobbyBuilder` is the central configuration point. Call methods like `UsePostgresql()`, `UseServerSettings()`, `UseDefaultRetryPolicy()`, then create client/server via `CreateJobbyClient()`/`CreateJobbyServer()`.

**Middleware Pipeline**: `IJobbyMiddleware` wraps job execution. Built-in middlewares: `MetricsMiddleware`, `TracingMiddleware`.

**Job Lifecycle States** (`JobStatus` enum):
- `Scheduled` → `Processing` → `Completed`/`Failed`
- `WaitingPrev` - waiting for predecessor in a sequence

### Storage Layer

The `IJobbyStorage` interface abstracts database operations. PostgreSQL implementation uses two tables:
- `jobby_jobs` - main job queue with UUIDv7 IDs
- `jobby_servers` - heartbeat tracking for distributed fault tolerance

### Test Projects (tests/)

- **Jobby.Tests.Core** - Unit tests with Moq
- **Jobby.Tests.AspNetCore** - ASP.NET Core unit tests
- **Jobby.Tests.Postgres** - PostgreSQL storage unit tests
- **Jobby.IntegrationTests.Postgres** - End-to-end tests requiring real PostgreSQL
- **Jobby.TestsUtils** - Shared fixtures and helpers
