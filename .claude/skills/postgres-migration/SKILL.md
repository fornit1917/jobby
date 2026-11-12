---
name: postgres-migration
description: Creating and evolving PostgreSQL schema migrations for Jobby.Postgres (Evolve SQL scripts, stored-function changes, keeping jobby.sql and C# commands in sync). Use when the user asks to add a DB migration / миграцию, change the jobby_jobs schema, or modify jobby stored procedures/functions.
---

# PostgreSQL Migrations for Jobby.Postgres

How to add a schema migration for the Jobby Postgres storage provider: file conventions,
placeholder variables, stored-function pitfalls, and everything that must stay in sync
with the migration.

## Where things live

- Migrations: `src/Jobby.Postgres/Migrations/*.sql` — plain SQL files, embedded resources
  (Evolve config: `EmbeddedResourceFilters = ["Jobby.Postgres.Migrations"]` in
  `src/Jobby.Postgres/PostgresqlJobbyStorageMigrator.cs`).
- Schema "source of truth" for fresh installs: `src/Jobby.Postgres/jobby.sql` —
  tables, indexes and the take-to-processing function. **Always update it in the same
  change as the migration.**
- Evolve metadata table: `jobby_evolve_migrations` (in the storage schema).

## File naming

```
V<MAJOR>_<MINOR>_<PATCH>_<yyyyMMdd>_<HHmm>__<ShortName>.sql
```

- The version part is the Jobby release the change ships in, e.g.
  `V1_0_0_20260218_2314__SchedulerType.sql`, `V1_1_0_20261112_1946__Traceparent.sql`
  (branch `dev-v1.1.0` → `V1_1_0_...`).
- The time part makes filenames sort chronologically within one release; take the current
  UTC time at the moment of writing.
- **Never edit an already-published migration file.** Existing databases have it recorded
  in `jobby_evolve_migrations`; a changed file either breaks their checksum or is silently
  skipped. Always add a new file.

## Placeholders

Substituted by `PostgresqlJobbyStorageMigrator` (see `Placeholders` in `Migrate()`).
Use them instead of hard-coding names, so schema/prefix settings work:

| Placeholder | Meaning |
|---|---|
| `${tables_prefix}` | prefix for table names (only needed when writing raw names) |
| `${jobs_table_fullname}` | full (schema-qualified) jobs table name |
| `${servers_table_fullname}` | servers table |
| `${unlocking_groups_table_fullname}` | unlocking groups table |
| `${take_to_processing_function_fullname}` | the take-batch function |

## Migration content rules

- Use `ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `DROP ... IF EXISTS` —
  keeps scripts idempotent and consistent with existing migrations.
- `CREATE OR REPLACE FUNCTION` only works when the function signature AND return type are
  unchanged. **Adding or altering a column in `RETURNS TABLE` raises
  `42P13: cannot change return type of existing function`.** In that case:
  ```sql
  DROP FUNCTION IF EXISTS ${take_to_processing_function_fullname}(TEXT, int, TEXT);
  CREATE FUNCTION ${take_to_processing_function_fullname}(...);
  ```
  (drop is safe: the function is recreated a few lines below). Note the drop takes
  argument types, not names. The same applies to `jobby.sql`.
- Changing a function body with identical signature/return type needs only
  `CREATE OR REPLACE` (see `V1_0_3_20260920_1600__FixStuckJobs.sql`).

## Keep the C# side in sync

A column added to `jobby_jobs` usually must flow through all of these:

1. **Insert commands** — `src/Jobby.Postgres/Commands/InsertJobCommand.cs` and
   `BulkInsertJobsCommand.cs`: column list, `VALUES` placeholders, parameter array
   (keep the `// N` position comments), and — if the value must change on re-creation of
   an exclusive recurrent job — the `ON CONFLICT DO UPDATE` branch. If the column must
   NOT be updated on upsert (e.g. it is always null for recurrent jobs), leave the branch
   untouched and say so in a comment/PR description.
2. **Read path** — `TakeBatchToProcessingCommand.cs` contains, besides the call to the
   stored function, a **fallback inline SQL** used when serializable groups are disabled
   (`_commandForDisabledSerializableGroups`): the new column must appear in its
   `RETURNING` (inner `updated` CTE) and in the outer `SELECT`. This query is easy to
   miss — it is not touched by the function migration.
3. **Reader mapping** — `src/Jobby.Postgres/Helpers/DataReaderExtensions.cs`
   (`GetJobAsync` reads columns by name; a missing column throws
   `ArgumentException: Column '...' does not exist` — unit tests with `FakeDbDataReader`
   catch this immediately).
4. **Models** — `JobCreationModel` (write), `JobExecutionModel` (read),
   `JobExecutionContext` (passed to handlers) in `src/Jobby.Core/Models`.
5. **Integration test helpers** — `JobDbModel` (EF model of `jobby_jobs`) in
   `tests/Jobby.IntegrationTests.Postgres/Helpers` plus `AssertHelper.AssertCreatedJob*`
   if the column must roundtrip.

## How to verify

PostgreSQL must be running (see "Finding a working docker invocation" in `AGENTS.md`;
on this machine: `wsl -u root docker-compose -f docker-compose.postgresql.yml up -d`).

1. `dotnet build` — zero warnings expected.
2. `dotnet test tests/Jobby.Tests.Postgres` — catches reader/schema drift cheaply
   (`FakeDbDataReader` fails on a column mismatch without any DB).
3. `dotnet test tests/Jobby.IntegrationTests.Postgres` — the fixture applies ALL
   migrations from scratch on every run, so a broken migration fails the whole suite in
   the fixture constructor (`EvolveException: Error executing script: <file>`); the error
   message contains the file name and the SQL that failed. `MigrationTests` additionally
   verifies migrations run twice cleanly and into a dedicated schema+prefix.
   - Manual from-scratch check (as in regression S2): drop all `jobby_*` tables INCLUDING
     `jobby_evolve_migrations`, restart the app, confirm migration activity in the startup
     log, then verify tables/function/indexes exist.
4. Evolve notes relevant to debugging: `OutOfOrder = true`, `TransactionMode = CommitAll`;
   migration scripts are recorded without prefix — that is why `MigrationTests` uses a
   dedicated schema for fresh-metadata runs.

## Checklist for a schema change

- [ ] New file in `src/Jobby.Postgres/Migrations/` following the naming convention
- [ ] `jobby.sql` updated (tables + function, `DROP FUNCTION` if return type changed)
- [ ] Insert commands updated (decide: is the column part of the upsert branch?)
- [ ] Inline fallback SQL in `TakeBatchToProcessingCommand` updated
- [ ] `DataReaderExtensions` / models / `JobExecutionContext` updated
- [ ] Integration test helpers (`JobDbModel`, `AssertHelper`) updated
- [ ] Unit tests (`FakeDbDataReader` rows) updated
- [ ] `dotnet build` + `tests/Jobby.Tests.Postgres` + `tests/Jobby.IntegrationTests.Postgres` green
- [ ] `versions.md` mentions the schema change if it is user-visible
