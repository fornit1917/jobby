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

If warnings appear while building the project, you should try to fix them.

Integration tests (`Jobby.IntegrationTests.Postgres`) need PostgreSQL. Start it with docker:
find a working docker invocation with the algorithm below, then run the compose file with it.

Connection settings: host `localhost`, port `5432`, user `jobby`, password `jobby`, database `jobby_tests_db`.

### Finding a working docker invocation

On a Windows host docker may be unavailable locally and only work inside WSL; the WSL
environment may also lack the compose plugin and deny docker access to the default user.
Walk through this algorithm until a command succeeds:

1. **Check where docker works.** Try in order, stop at the first success:
   - locally: `docker ps`
   - in WSL as the default user: `wsl docker ps`
   - in WSL as root: `wsl -u root docker ps` (on this repo's machine the default WSL user
     gets "permission denied" on the docker socket — root works)
   If every variant fails with "cannot connect to the Docker daemon" (not "command not
   found"), the daemon is down. Starting it inside WSL requires an interactive password
   prompt (`sudo service docker start`) — ask the user to run it, e.g. by suggesting
   they type `! wsl sudo service docker start`. If a variant fails with "command not
   found", that location has no docker; move on.

2. **Check which compose command exists** in the location that worked (`<prefix>` is
   empty locally, `wsl` or `wsl -u root` for WSL):
   - `<prefix> docker compose version` — modern compose plugin
   - `<prefix> docker-compose version` — legacy standalone binary (this repo's WSL has
     only this one)
   Use whichever answered; if both answer, prefer `docker compose`.

3. **Start PostgreSQL** with the working invocation:

   ```bash
   <prefix> <compose-command> -f docker-compose.postgresql.yml up -d
   # example for this repo's machine:
   wsl -u root docker-compose -f docker-compose.postgresql.yml up -d
   ```

4. **Verify** the DB is up and the port is published:
   `<prefix> docker ps --format "{{.Names}} {{.Status}} {{.Ports}}"` should list
   `jobby_pg` with `0.0.0.0:5432->5432/tcp`. Then run the integration tests
   (`dotnet test tests/Jobby.IntegrationTests.Postgres`).

## Regression Testing

A manual regression suite for the library (26 scenarios: jobs, queues, groups, sequences,
recurrent jobs, retry, heartbeat, fault tolerance, metrics, tracing, freezing) lives in
`.claude/skills/regression-testing/SKILL.md`. When asked to run regression
testing / регрессионное тестирование, read and follow `SKILL.md` exactly: it runs a disposable
copy of `samples/Jobby.Samples.AspNet` against PostgreSQL in docker and never modifies
anything outside `temp/`.

CI (`.github/workflows/dotnet.yml`) builds on .NET 8.0.x, starts the compose file above and runs `dotnet test --no-build`.

## Unit Test Guidelines

Rules that keep the test suites useful; all of them exist because a violation was actually found here.

### Structure

- **One test class per tested type, named after it.** Don't add tests for `DbName` into
  `PostgresqlStorageBuilderTests` or tests for `PostgresqlStorageSettings` into a builder's
  test class. If a test class for the target type exists, add there; otherwise create
  `<TypeName>Tests.cs` next to it (same folder layout as `src/`).
- Before adding a test, check it doesn't duplicate an existing one in the same class or
  in a neighbouring class covering the same type.
- A helper (fixture, fake, tracker) lives in its own file next to its consumers, not
  appended to an unrelated test file.

### Meaningfulness

- **Every assert must be able to fail** in a way that detects the regression the test
  targets. Forbidden patterns:
  - `Assert.NotNull(x)` right after constructing `x` — proves nothing;
  - `Assert.ThrowsAny<Exception>` — pin the concrete exception type and check the message;
  - asserts that are trivially true for any realistic state (e.g. checking a string id is
    not `Guid.Empty` when the value comes from a DB row that must exist anyway).
- A test must not claim more than it asserts. If the name says "applies schema and prefix"
  but the body only checks that an object is not null, delete the test or move the
  verification to the level where it can actually fail (e.g. integration tests).
- Pin tests (locking in known-rough current behaviour) must say so explicitly in a comment,
  including what a future fix would change.

### Determinism

- No test may depend on state left by another test unless that state is established
  explicitly within the test. Shared static state must be reset safely: a reset may run
  while a previous test's background work is still in flight (wait for it, e.g.
  `SpinWait.SpinUntil`).
- Don't assert intermediate state at an arbitrary point in time: a job that cycles
  between `Scheduled` and `Processing` may be in either at the moment of the check —
  assert on invariants (execution counts, ordering, stored values) instead.
- Timing-based asserts (`Stopwatch`, `Task.Delay` windows) must have margins that make
  them stable on slow machines, and must test a real behaviour (polling, backpressure),
  not just pass by construction.
- Roundtripped data loses what the serializer can't preserve: an exception thrown inside
  a job reaches the DB via JSON `job_param`, so its message does not survive — assert on
  the fact of the error, not its exact text.

### Async

- In async methods use `await using` for `IAsyncDisposable` objects (`DbContext`,
  `DbDataReader`, `NpgsqlConnection`, `ServiceProvider`). Plain `using` is fine in sync
  methods and for `IDisposable`-only types (e.g. `CancellationTokenSource`).
- In xunit.v3 tests, pass `TestContext.Current.CancellationToken` to cancellable awaits.

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
- In async methods use `await using` (not `using`) for `IAsyncDisposable` objects — `DbContext`, `DbDataReader`, `NpgsqlConnection`, `ServiceProvider`, etc. Plain `using` is fine in synchronous methods, and for types that only implement `IDisposable` (e.g. `CancellationTokenSource`).
- Keep the public API stable across patch releases; deprecate before removing (see the v1.0.0 removals in `versions.md` for examples).
