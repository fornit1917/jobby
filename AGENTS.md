# AGENTS.md

Guidance for AI coding agents working on this repository.

## Project Overview

Jobby is a high-performance .NET library for background tasks (queued, scheduled and recurring jobs), designed for distributed applications. Published as NuGet packages: `Jobby.Core`, `Jobby.Postgres`, `Jobby.AspNetCore`.

- Documentation site: https://fornit1917.github.io/jobby/
- Targets .NET 8 (`net8.0`).

## Repository Layout

- `src/Jobby.Core` — core library: job models, interfaces, scheduling, retry policies, middlewares, server/client services.
- `src/Jobby.Postgres` — PostgreSQL storage provider (uses Npgsql, Evolve for migrations).
- `src/Jobby.AspNetCore` — ASP.NET Core / DI integration.
- `tests/Jobby.Tests.Core`, `tests/Jobby.Tests.Postgres`, `tests/Jobby.Tests.AspNetCore` — unit tests (xunit.v3, Moq).
- `tests/Jobby.IntegrationTests.Postgres` — integration tests, require a running PostgreSQL.
- `tests/Jobby.TestsUtils` — shared test utilities.
- `samples` — runnable example applications.
- `benchmarks/Jobby.Benchmarks` — BenchmarkDotNet benchmarks.
- `docs` — VitePress documentation site (English in `docs/docs`, Russian in `docs/ru`).

## Build and Test

```bash
dotnet build                # build the solution
dotnet test                 # run all tests (includes integration tests)
```

Integration tests (`Jobby.IntegrationTests.Postgres`) need PostgreSQL. Start it with:

```bash
docker compose -f docker-compose.postgresql.yml up -d
```

Connection settings: host `localhost`, port `5432`, user `jobby`, password `jobby`, database `jobby_tests_db`.

## Regression Testing

A manual regression suite for the library (26 scenarios: jobs, queues, groups, sequences,
recurrent jobs, retry, heartbeat, fault tolerance, metrics, tracing, freezing) lives in
`.claude/skills/regression-testing/SKILL.md`. When asked to run regression
testing / регрессионное тестирование, read and follow `SKILL.md` exactly: it runs a disposable
copy of `samples/Jobby.Samples.AspNet` against PostgreSQL in docker and never modifies
anything outside `temp/`.

CI (`.github/workflows/dotnet.yml`) builds on .NET 8.0.x, starts the compose file above and runs `dotnet test --no-build`.

### Documentation

```bash
npm install
npm run docs:dev     # local VitePress dev server
npm run docs:build   # build docs
```

## Conventions

- User-facing docs exist in both English (`docs/docs`) and Russian (`docs/ru`); keep both in sync when changing documentation.
- Release notes are maintained in `versions.md` (one `## vX.Y.Z (YYYY-MM-DD)` section per release).
- NuGet publishing is done via `./publish-nuget.sh <VERSION>` (requires `NUGET_KEY`); all three packages are versioned together.
- Breaking or notable API changes should be reflected in `versions.md`.

## Code Style

- Follow the surrounding C# style: file-scoped conventions and naming consistent with existing code in `src/`.
- Keep the public API stable across patch releases; deprecate before removing (see the v1.0.0 removals in `versions.md` for examples).
