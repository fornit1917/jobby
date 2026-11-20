---
name: regression-testing
description: Manual regression testing of the Jobby library through its ASP.NET Core demo app. Runs the full scenario suite (jobs, queues, groups, sequences, recurrent jobs, retry, heartbeat, fault tolerance, metrics, tracing, freezing). Use when the user asks to run regression testing / регрессионное тестирование of Jobby.
---

# Jobby Regression Testing

Run the full regression suite of the Jobby library by exercising a disposable copy of the demo
app (`samples/Jobby.Samples.AspNet`) against a PostgreSQL instance started via docker-compose.

**Key principle: never modify anything under `samples/`, `src/`, or `tests/` during testing.**
All temporary code/config changes are made inside the disposable copy in `temp/` which is
deleted after the run.

## Overview of the flow

1. Environment check (docker availability — ask the user if the daemon is not running)
2. Start/clean the database
3. Create the disposable copy of the demo app in `temp/`
4. Run the scenarios (below), editing only the copy's config/code per-scenario
5. Produce a pass/fail report
6. Cleanup: stop app instances, truncate tables, delete the copy

Reference documents (read if in doubt):
- User docs: `docs/docs/*.md` (fault-tolerance, sequential-execution, jobs-schedule, observability)
- DB schema: `src/Jobby.Postgres/jobby.sql`

## 1. Environment check

### 1.1 Docker (via WSL)

> **Environment note:** this and all other `wsl ...` wrappers in this skill describe the
> WSL-based setup (Windows host, docker/psql available only inside WSL). If the skill runs
> in an environment where docker and psql are available directly on the host (Linux, or
> Windows with native docker/psql), ignore the WSL wrappers: run the docker/psql commands
> natively, and treat the `wsl -u root` escalation as unnecessary.

Docker and psql are NOT available on the Windows host — only inside WSL (Debian).
All docker/psql commands must be run as:

```
wsl -e bash -lc "<command>"
```

Note: the repo root in WSL is `/mnt/d/projects/jobby`.

Check in this order:

1. `wsl -e bash -lc "docker --version"` — if this fails, tell the user docker is not installed
   in WSL and stop.
2. `wsl -e bash -lc "docker ps"` — if it fails with `Cannot connect to the Docker daemon`,
   **stop and ask the user** to start it by typing in the session:
   `! wsl -e bash -lc "sudo service docker start"`
   Do not try to start the daemon yourself (sudo requires a password).
3. If it fails with `permission denied` on `/var/run/docker.sock` (daemon IS running, but the
   WSL user is not in the `docker` group), use `wsl -u root -e bash -lc "..."` for all docker
   and psql commands — WSL allows launching as root without a password. Verify it works; only
   if that also fails, ask the user.
3. Check the DB container: `wsl -e bash -lc "docker ps -a | grep jobby_pg"`.
   - If it exists but is stopped: `docker-compose -f docker-compose.postgresql.yml start` —
     docker-compose v1 requires `-f` for every subcommand when the file is not named
     `docker-compose.yml` (run from the repo root in WSL: `/mnt/d/projects/jobby`).
     The `docker compose` plugin is NOT available.
   - If it does not exist: `wsl -e bash -lc "cd /mnt/d/projects/jobby && docker-compose -f docker-compose.postgresql.yml up -d"`
4. Wait for readiness: `wsl -e bash -lc "pg_isready -h localhost -p 5432"` → expect `accepting connections`.

Connection details: host `localhost`, port `5432`, user `jobby`, password `jobby`,
database `jobby_tests_db`. The Windows-side app connects to `localhost:5432` (WSL2 forwards the port).

### 1.2 psql helper

Run all SQL as:

```
wsl -e bash -lc "PGPASSWORD=jobby psql -h localhost -U jobby -d jobby_tests_db -c '<SQL>'"
```

Useful status values (`JobStatus`): `Scheduled=1, Processing=2, Completed=3, Failed=4,
WaitingPrev=5, Frozen=6`.

Typical inspection query:

```sql
SELECT id, job_name, status, started_count, queue_name, serializable_group_id,
       is_group_locker, can_be_restarted, server_id, error
FROM jobby_jobs ORDER BY created_at;
```

## 2. Create the disposable app copy

Copy the sample to **exactly `temp/Jobby.Samples.AspNet`** (depth 1 under the repo root).
The depth matters: the csproj references `..\..\src\Jobby.*.csproj`; from `temp/<name>/`
these still resolve to the repo's `src/`. Any deeper (e.g. `temp/regression/app/`) breaks the build.

```bash
rm -rf temp/Jobby.Samples.AspNet
cp -r samples/Jobby.Samples.AspNet temp/Jobby.Samples.AspNet
rm -rf temp/Jobby.Samples.AspNet/bin temp/Jobby.Samples.AspNet/obj
```

Store all runtime artifacts (logs, snapshots) in `temp/regression/`.

Build the copy once:

```bash
dotnet build temp/Jobby.Samples.AspNet/Jobby.Samples.AspNet.csproj
```

## 2.1 Speed setup (do once, right after copying the app)

Most of the suite's wall-clock time is fixed sleeps and kill→edit→build→start cycles.
Applying this section once cuts the run roughly in half. The substitutions here override
the literal waits written in the scenarios below.

### Helper commands (copy them, don't re-derive)

The psql wrapper below is WSL-specific (it shells out through `wsl -u root`) — create it
only in the WSL environment described in §1.1, i.e. when docker/psql are available only
inside WSL. If psql is available on the host directly, skip the wrapper and run
`PGPASSWORD=jobby psql -h localhost -U jobby -d jobby_tests_db -c "<SQL>"` natively
(same for the `docker-compose` and `pg_isready` calls — drop the `wsl ...` prefix).

Create the psql wrapper once and route every SQL call through it:

Standard snippets used repeatedly throughout the suite:

```bash
# wait until an instance is ready (~1 s; poll, don't guess a sleep)
until [ "$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:5075/metrics)" = 200 ]; do sleep 0.5; done

# kill the instance serving a port
taskkill //F //PID $(netstat -ano | grep ":5075" | grep LISTEN | awk '{print $NF}' | head -1)

# wait for a log line (poll instead of a fixed sleep; <file> = instance log)
until grep -q "Lost server" <file> 2>/dev/null; do sleep 1; done

# wait until a DB state appears (poll; adjust the WHERE)
until bash temp/regression/db.sh "SELECT 1 WHERE EXISTS (SELECT 1 FROM jobby_jobs WHERE status=4)" | grep -q 1; do sleep 1; done
```

Restart-after-edit depends on WHAT was edited:

```bash
# --- config-only edit (appsettings.json): NO REBUILD. ---
# dotnet run serves appsettings.json from the PROJECT dir, so the new config applies
# on a plain process restart; --no-build guarantees nothing touches the output dir
# and no accidental build sneaks in:
taskkill //F //PID $(netstat -ano | grep ":5075" | grep LISTEN | awk '{print $NF}' | head -1)
# ...edit the copy's appsettings.json...
dotnet run --project temp/Jobby.Samples.AspNet --no-launch-profile --no-build --urls http://127.0.0.1:5075 \
  >> temp/regression/logs/instance1.log 2>&1 &
# run the wait-ready loop above

# --- code edit (Program.cs, DemoJobCommand, ...): build IS required. ---
# never build while an instance is running (output files are locked, see §5 discipline):
taskkill //F //PID $(netstat -ano | grep ":5075" | grep LISTEN | awk '{print $NF}' | head -1)
# ...edit the copy's code...
dotnet build temp/Jobby.Samples.AspNet/Jobby.Samples.AspNet.csproj 2>&1 | tail -4   # expect "Ошибок: 0"
# start instance in background, then run the wait-ready loop above
```

When unsure whether a change is config or code: `UseServerSettings` lives in `Program.cs`
(code — rebuild), `Jobby:` section lives in `appsettings.json` (config — restart only).
The config-only path applies to S24 tracing flags, `Jobby:UseMetrics`, etc.

### Fast server intervals — set them ALL in the initial copy

Edit the copy's `UseServerSettings` ONCE at setup to include every scenario-tuned value,
so S15–S18 need no extra rebuild/restart cycles:

```csharp
PermanentLockedFreezingIntervalSeconds = 2,
PermanentLockedHandleUnlockingRequestsIntervalSeconds = 2,
HeartbeatIntervalSeconds = 2,
MaxNoHeartbeatIntervalSeconds = 6,
DbErrorPauseMs = 2000
```

Accepted side effect: blocked groups now freeze after ~2 s everywhere, so S14B's second
job transitions `Scheduled`→`Frozen` quickly — fine, the assertion is only that it never
executes. S9 still needs its own restarts (it tests other settings' values).

### Pre-add the S17 code edit

Add the `CanBeRestarted` field to the copy's `DemoJobCommand` right after copying (see S17
for the exact `JobOpts.CanBeRestartedIfServerGoesDown` mapping) — one rebuild at setup
instead of a mid-suite rebuild+restart.

### Immediate DB checks (the S24 pattern, applies everywhere)

With `DeleteCompleted=true` a fresh job row is visible in the DB from the moment of
creation until completion. To verify row fields without racing the completion, do NOT
sleep first: give the job a small `DelayMs≈1500`, POST it, then poll the DB until the row
appears (~1 s at 500 ms polling — it is in status 1 or 2 then) and check the fields.
Poll the log for the execution line only when you need execution evidence, not for DB
checks. A fixed `sleep` before a checkable state is always wrong: poll for the state.

### Timing substitutions (override the scenario sleeps)

The server polls every 500 ms, so new state is detectable almost immediately; the scenario
sleeps are conservative. Poll instead of sleeping:

| Where | Literal wait | Replace with |
|---|---|---|
| S1/S21 | wait ~7 s | poll the log until 2–3 "executed" lines appear, then check DB |
| S4/S20/S22 | `StartAfter` now+15..20 s | now+4 s, poll for the execution line / row removal |
| S12/S13/S14/S19 | `sleep 10` after enqueue | poll DB until `status=4` appears, then verify details |
| S15 | `sleep 8` / `sleep 6` | poll until all ready jobs are `status=6`; poll for the unlock log line |
| S16/S17 | `DelayMs=20000` | `DelayMs=8000`; kill the executor as soon as `status=2` is observed |
| S16 | `sleep 12` after kill | poll the survivor's log for `Lost server` |
| S16 | `sleep 24` + `sleep 8` for completion | poll until the job row disappears |
| S18 | `DelayMs=20000` | `DelayMs=8000`; stop postgres right after `status=2` is seen |
| S18 | observe DB errors ~15 s | poll the log until 3+ DB-error lines appear (~2 s apart at `DbErrorPauseMs=2000`), then recover postgres |

Cap every poll loop at the scenario's stated timeout and count the iterations, so a
never-met condition is reported as a FAIL, not a hang.

### Overlap independent scenarios with waits

While a scenario parks a delayed job (S4) or waits out retries (S12/S13/S14), run the next
independent scenario's enqueues and checks meanwhile — log/DB checks are value- or
group-scoped and don't interfere. Never overlap scenarios that edit config/code, restart,
or kill instances.

## 3. Clean the database

Before the run (and between scenario groups):

```sql
TRUNCATE jobby_jobs;
TRUNCATE jobby_servers;
TRUNCATE jobby_unlocking_groups;
```

## 4. Running and stopping the app

Run each instance in the background with a fixed URL and redirect stdout to a log file —
**stdout is the primary observation channel** (job logs, middleware logs, tracing exporter):

```bash
dotnet run --project temp/Jobby.Samples.AspNet --no-launch-profile --urls http://127.0.0.1:5075 \
  > temp/regression/logs/instance1.log 2>&1
```

- Port 5075 for the first instance, 5076 for the second (heartbeat scenarios).
- `--no-launch-profile` is required to avoid the launchSettings port and browser autolaunch.
- Each instance registers its own row in `jobby_servers` with a random server id.
- On startup the app runs migrations and schedules an exclusive recurrent job
  (`EmptyRecurrentJobCommand`, cron `*/5 * * * * *`) — its executions every 5 s in the log are normal.
- Wait for readiness: poll `curl -s http://127.0.0.1:5075/metrics -o /dev/null -w "%{http_code}"`
  until it returns 200.
- Stop instances by killing the dotnet process (`taskkill //F //PID <pid>` on Windows, or kill the
  background shell). Track PIDs yourself when you start them.

## 5. API of the app under test

All endpoints are `POST` unless stated; base URL `http://127.0.0.1:5075` (or :5076).

| Endpoint | Body | Effect / returns |
|---|---|---|
| `/jobs/enqueue-job` | `DemoJobCommand` JSON | enqueues a job, returns jobId (GUID) |
| `/jobs/enqueue-job-by-ef` | `DemoJobCommand` JSON | same but created via EF Core |
| `/jobs/enqueue-batch` | array of `DemoJobCommand` | transactional batch, returns array of jobIds |
| `/jobs/enqueue-sequence` | array of `DemoJobCommand` | chain via `JobsSequenceBuilder`, returns jobIds in chain order |
| `/jobs/cancel-job/{jobId}` | — | cancels job by id |
| `/jobs/schedule-recurrent` | cron string, e.g. `"*/2 * * * * *"` | schedules exclusive recurrent job (cron) |
| `/jobs/schedule-recurrent-with-interval` | number (seconds) | recurrent job with built-in `TimeSpanSchedule` |
| `/jobs/schedule-recurrent-with-custom-scheduler` | number (seconds) | recurrent job with custom `SecondsIntervalSchedule` |
| `/jobs/cancel-recurrent` | — | cancels exclusive recurrent `EmptyRecurrentJobCommand` |
| `/jobs/cancel-recurrent-with-custom-scheduler` | — | cancels custom-scheduler recurrent job |
| `/metrics` (GET) | — | Prometheus metrics |

`DemoJobCommand` fields:

```json
{
  "Value": "text logged by the handler",
  "ShouldBeFailed": false,
  "ShouldThrowIgnoredException": false,
  "StartAfter": null,
  "DelayMs": 0,
  "QueueName": null,
  "SerializableGroupId": null,
  "LockGroupIfFailed": false,
  "TracingMode": null
}
```

Handler behavior: logs `Executing demo job, value is {Value}, attempt number is {Attempt}`;
`ShouldBeFailed=true` throws (retried per retry policy); `ShouldThrowIgnoredException=true`
throws an exception that `IgnoreSomeErrorsMiddleware` swallows; `DelayMs` delays completion.
`TracingMode` (`"RestoreTraceId" | "LinkTraceId" | "CreateNewTraceId"`, string enum —
`JsonStringEnumConverter` is applied on the sample's command) overrides the global
`Jobby:TracingMode` appsettings value for that job; used by S24.

Default demo config (`Program.cs` of the copy): polling 500 ms; retry policy `MaxCount=3,
IntervalsSeconds=[1,2]`; queues: `default`, `recurrent`, `heavy` (`MaxDegreeOfParallelism=1`);
`DeleteCompleted=true` (default), `CompleteWithBatching=false` (default).

You may edit **the copy's** `Program.cs` / `appsettings.json` / code per-scenario
(e.g. server settings, extra command fields). That is expected — the repo itself stays untouched.
Code edits require a rebuild; appsettings.json edits require only a process restart
(`dotnet run --no-build` — see the two restart paths in §2.1).

**Rebuild discipline (important — bitten in practice):**
- NEVER build while an instance is running: the running exe locks the output and the build
  fails with MSB3027/MSB3021 (after ~13 s of MSBuild copy retries). Kill instances first.
- After every build, verify success explicitly (check for `error`/`ошибок` lines — do not rely
  on truncated output) and confirm the output dll timestamp is newer than the edited sources.
  A silently failed build leaves stale binaries whose symptoms are subtle (e.g. a new JSON
  field missing from serialized `job_param`).
- After starting an instance, verify it is actually a NEW process: `dotnet run` on an
  occupied port fails to bind while an OLD instance keeps serving the port — `curl` returning
  200 does not prove your new instance is the one answering.

## 6. Scenarios

Run them in the listed order. Before each scenario (or group): truncate `jobby_jobs`
(`jobby_servers`/`jobby_unlocking_groups` when relevant), restart instances if config changed.
For every scenario record: what was done, observed evidence (log excerpts, DB rows, HTTP
responses), and verdict PASS/FAIL.

### S1. Recurrent job with cron schedule
1. `POST /jobs/schedule-recurrent` with body `"*/2 * * * * *"`.
2. Wait ~7 s; observe log: `EmptyRecurrentJob` executions every ~2 s.
3. DB: exactly one row `job_name='EmptyRecurrentJob'`, `schedule='*/2 * * * * *'`,
   `started_count` growing, `status` oscillating `Scheduled`/`Processing`, single `queue_name='recurrent'`.
**Expected:** periodic executions with no duplicates; one row.

### S2. Cancel recurrent job
1. Continue from S1. `POST /jobs/cancel-recurrent`.
2. Wait ~7 s. DB: the recurrent row is gone (or permanently inactive) — no new executions in log.
**Expected:** executions stop after cancel; no new rows appear.

### S3. Plain job execution
1. `POST /jobs/enqueue-job` `{"Value":"s3","DelayMs":500}`.
2. Log: handler executed once (`attempt number is 1`); middleware logs started/completed.
3. DB (query immediately): row status `Processing`, then (DeleteCompleted=true) row deleted
   after success.
**Expected:** executed exactly once, completed.

### S4. Job with scheduled start time
1. `POST /jobs/enqueue-job` `{"Value":"s4","StartAfter":"<now+15s UTC ISO>"}` (e.g. `2026-01-01T00:00:10Z` style).
2. Immediately: DB row `status=1` (Scheduled), `scheduled_start_at` ≈ requested time; log has
   NO execution yet.
3. After the time passes: executed once; row deleted (DeleteCompleted=true).
**Expected:** not executed before `StartAfter`, executed once after.

### S5. Jobs in different queues
1. Enqueue two jobs with `"QueueName":"heavy"` and one job to the default queue (any `DelayMs`).
2. All three jobs execute and complete.
3. DB during execution: rows with `queue_name` as requested.
**Expected:** jobs from all configured queues are picked up and executed (do NOT assert
   per-queue `MaxDegreeOfParallelism` behavior — known to be broken, out of scope).

### S6. Job creation via EF Core
1. `POST /jobs/enqueue-job-by-ef` `{"Value":"s6"}`.
2. Verify the job executes like S3.
**Expected:** identical behavior to a normally enqueued job (the row was inserted by EF).

### S7. Serializable group: sequential execution
1. Enqueue 3 jobs with `"SerializableGroupId":"g7","DelayMs":1500`.
2. From log timestamps confirm: no two jobs of the group overlap; executed in creation order.
3. DB during run: only one group job at a time has `status=2`; others `status=1`.
**Expected:** strict one-at-a-time execution within the group.

### S8. Metrics
1. `curl http://127.0.0.1:5075/metrics` — contains `jobby_inst_jobs_started`,
   `jobby_inst_jobs_completed`, `jobby_inst_jobs_retried`, `jobby_inst_jobs_failed`,
   `jobby_inst_jobs_duration` (Prometheus naming, `# TYPE` lines present).
2. Run a couple of jobs (S3) and re-fetch: `started`/`completed` counters increased.
3. Run one failing job (`ShouldBeFailed=true`): `retried` and `failed` counters increase.
**Expected:** all five metric families present and reflecting activity.

### S9. Server settings variations (DeleteCompleted / CompleteWithBatching / queue count)
Run three sub-checks, editing the copy's `UseServerSettings` in `Program.cs` between them
(these are CODE edits — each sub-check needs the kill→edit→build→start cycle, the
appsettings no-rebuild shortcut does NOT apply here):
1. `DeleteCompleted=false`: successful jobs remain in DB with `status=3`.
2. `CompleteWithBatching=true`: jobs still complete correctly (batch completion path works);
   with `DeleteCompleted=false` rows end `status=3`, with `true` rows are removed.
3. Single queue only (`Queues=[default]`): default-queue jobs execute; jobs sent to `heavy`
   stay `Scheduled` and are NOT executed (server is not listening to that queue).
**Expected:** settings behave per `docs/docs/install-and-config.md` semantics.

### S10. Transactional batch
1. `POST /jobs/enqueue-batch` with 5 commands `{"Value":"s10-N","DelayMs":300}`.
2. DB right after the call: all 5 rows already present (single transaction).
3. All 5 execute and complete.
**Expected:** atomic visibility, all jobs complete.

### S11. Sequence chain (SequenceBuilder)
1. `POST /jobs/enqueue-sequence` with 3 commands `{"Value":"s11-N","DelayMs":800}`.
2. From log timestamps: strict order s11-1 → s11-2 → s11-3, no overlap, next starts only
   after previous completes. DB: first job `status` transitions 2→(3/deleted); others wait
   `status=5` (WaitingPrev) until their turn.
**Expected:** strict chain order.

### S12. Failure in the middle of a sequence
1. `POST /jobs/enqueue-sequence` with `{"Value":"s12-1"}`,
   `{"Value":"s12-2","ShouldBeFailed":true}`, `{"Value":"s12-3"}`.
2. s12-1 completes; s12-2 exhausts retries (3 attempts per default policy) → `status=4`;
   s12-3 NEVER executes, stays `status=5` (WaitingPrev).
**Expected:** chain halted by failed job; no retries continue past failure.

### S13. Retry policy on failure
1. `POST /jobs/enqueue-job` `{"Value":"s13","ShouldBeFailed":true}`.
2. Log: 3 attempts total (`attempt number is 1/2/3`), delays ≈1 s then ≈2 s between attempts;
   middleware logs `failed and will be restarted` for attempts 1–2 and `failed` on the last.
3. DB final row: `status=4`, `started_count=3`, `error` populated.
**Expected:** default policy (3 attempts, [1,2] s) observed exactly.

### S14. Serializable group lock on failure (`lock_group_is_failed`)
1. Sub-case A (no lock): enqueue `{"SerializableGroupId":"g14","ShouldBeFailed":true}` and
   then `{"SerializableGroupId":"g14","Value":"after"}`. After retries exhaust, `status=4`,
   and the second job still executes (group not blocked).
2. Sub-case B (lock): same but first job has `"LockGroupIfFailed":true`. After failure the
   first row keeps `is_group_locker=true`; the second job stays `Scheduled` and never executes.
**Expected:** A — group continues; B — group blocked by failed locker.

### S15. Freezing and unfreezing of permanently locked groups
1. Both settings are already applied by the §2.1 speed setup — no edit/restart needed,
   just make sure an instance is running.
2. Create a locked group with queued work: `{"SerializableGroupId":"g15","ShouldBeFailed":true,"LockGroupIfFailed":true}`
   then 5 jobs `{"SerializableGroupId":"g15","Value":"frozen-N"}`.
3. After the first job fails: ~2–5 s later the 5 ready jobs transition `status=6` (Frozen).
4. Unlock via SQL (inserts an unlocking request handled by the server):
   `INSERT INTO jobby_unlocking_groups (group_id, created_at) VALUES ('g15', now());`
5. Within a few seconds: frozen jobs return to `Scheduled` and execute; log shows
   `Serializable group g15 has been unlocked`. The failed locker row is DELETED after the full
   unlock — this is by design (the `deleted_locker` step in the unlocking SQL,
   `PostgresqlPermanentLocksStorage.cs`), not a bug.
**Expected:** automatic freezing of large locked groups; SQL unlock request is honored;
   locker removed after unlock.

### S16. Heartbeat: server removal and stuck-job restart
1. Heartbeat settings are already applied by the §2.1 speed setup — no edit/restart needed.
2. Start instance 1 (:5075). DB `jobby_servers`: a row appears; `heartbeat_ts` keeps updating
   (re-query twice ~2 s apart).
3. Start instance 2 (:5076). Two rows now.
4. Enqueue `{"Value":"s16","DelayMs":8000}` via instance 1; poll until its row shows
   `status=2` and note its `server_id`. Then check WHICH instance is actually executing it
   (grep the job's `Value` in each instance's log — either instance may pick it up, since both
   poll the same queue).
5. Kill the instance that is executing the job (hard kill, no graceful shutdown) IMMEDIATELY —
   the job's delay is short now, so don't dawdle between step 4 and this kill.
6. Poll the surviving instance's log (up to ~12 s) for:
   `Lost server was found and deleted, serverId = <killed instance id>`.
   DB: killed instance's row gone from `jobby_servers`; the job restarted on the surviving
   instance (`server_id` changes). NOTE: `started_count` is DECREMENTED by design on restart
   (`FindAndRestartStuckJobsCommand`: `started_count = started_count - 1`), so after re-take it
   reads 1 again and the handler logs `attempt number is 1` — this is correct behavior, not a
   bug. After the delay the job completes and (DeleteCompleted=true) the row is deleted.
**Expected:** dead server evicted by heartbeat timeout; its in-flight job restarted on a live instance.

### S17. `CanBeRestartedIfServerGoesDown=false`
1. Same settings/instances as S16. The `CanBeRestarted` field was already added to the
   copy's `DemoJobCommand` by the §2.1 speed setup (mapped to
   `JobOpts.CanBeRestartedIfServerGoesDown`) — no rebuild needed; just TRUNCATE
   `jobby_jobs` and ensure both instances are running.
2. Enqueue `{"Value":"s17","DelayMs":8000,"CanBeRestarted":false}` to instance 1; poll until `status=2`.
3. Kill instance 1. Poll up to ~12 s: server removed as in S16, but the job is NOT restarted —
   it stays with the dead server's `server_id`, `started_count` stays 1, `can_be_restarted=false`.
**Expected:** non-restartable job is left untouched after its server dies.

### S18. Database becomes unavailable during job execution
1. `DbErrorPauseMs=2000` is already applied by the §2.1 speed setup — no edit/restart needed.
2. Enqueue `{"Value":"s18","DelayMs":8000}`; poll until `status=2`, then IMMEDIATELY stop
   postgres — the delay is short now, so don't dawdle between steps.
3. `wsl -e bash -lc "cd /mnt/d/projects/jobby && docker-compose stop"` (stops postgres).
4. Observe instance-1 log for ~15 s: repeated DB errors
   (`Error receiving next jobs from queue` and/or errors from the completion/post-processing path)
   every ~2 s — the server keeps retrying to persist the completion of the finished job.
   IMPORTANT: the job's delay must elapse while the DB is down (finish the enqueue→stop
   postgres sequence well within the delay), otherwise the completion is persisted normally
   and the pending-completion path is not tested — redo the scenario if that happens.
5. `wsl -e bash -lc "cd /mnt/d/projects/jobby && docker-compose start"`; wait for pg_isready.
6. Poll: within a few seconds the pending completion is applied: job row completed/deleted per
   `DeleteCompleted`; polling resumes without errors.
**Expected:** no data loss; the completed job's final status is persisted after DB recovery;
   the server pauses and retries at `DbErrorPauseMs` while the DB is down.

### S19. Middlewares pipeline
1. Default config of the copy has both middlewares. Enqueue
   `{"Value":"s19a","ShouldThrowIgnoredException":true}` → log shows handler throwing, but
   `IgnoreSomeErrorsMiddleware` swallows it: job completes (no retry, no `failed` log), status
   Completed (or row deleted).
2. Enqueue `{"Value":"s19b","ShouldBeFailed":true}` → `JobLoggingMiddleware` logs
   `failed and will be restarted` on attempts 1–2 and `failed` on attempt 3 (LogError).
**Expected:** middleware order/behavior: ignored exception → success; real failure → retries.

### S20. Cancel a delayed (not yet started) job
1. `POST /jobs/enqueue-job` `{"Value":"s20","StartAfter":"<now+20s>"}`; note jobId.
2. `POST /jobs/cancel-job/{jobId}`.
3. Wait 25 s: no execution in log; DB row removed (or permanently inactive).
**Expected:** cancelled job never executes.

### S21. Exclusive recurrent job: re-schedule updates instead of duplicating
1. `POST /jobs/schedule-recurrent` `"*/3 * * * * *"`, then again with `"*/7 * * * * *"`.
2. DB: still exactly ONE `EmptyRecurrentJob` row (with `IsExclusive` semantics), schedule now
   `*/7 * * * * *`; log cadence changes to ~7 s.
**Expected:** second call updates the schedule, no duplicate created.

### S22. Group ordering by scheduled start time
1. Enqueue 3 jobs to the same queue with `SerializableGroupId="g22"`, `DelayMs=500`, and
   distinct `StartAfter` (now+2s, now+1s, now+3s — deliberately out of order).
2. Log order of executions must follow ascending `scheduled_start_at` (s22-2, s22-1, s22-3),
   one at a time.
**Expected:** within one queue the group executes in ascending scheduled-start order.

### S23. Migrations on a wiped database
1. Stop all app instances.
2. Wipe the entire jobby schema via psql (including Evolve's metadata table, so migrations
   re-run from scratch):
   ```sql
   DROP TABLE IF EXISTS jobby_jobs, jobby_servers, jobby_unlocking_groups,
                         jobby_evolve_migrations CASCADE;
   DROP FUNCTION IF EXISTS jobby_take_to_processing(TEXT, INT, TEXT) CASCADE;
   ```
   Verify nothing is left: `\dt jobby_*` returns no tables.
3. Start instance 1. The app runs `IJobbyStorageMigrator.Migrate()` (Evolve) at startup —
   the startup log must show migration activity.
4. Verify the schema was fully recreated: all three tables exist (`\dt jobby_*`),
   the take-function exists (`SELECT jobby_take_to_processing('default', 1, 'probe');` — returns
   0 rows without error), indexes exist (`\d jobby_jobs` shows the indexes from
   `src/Jobby.Postgres/jobby.sql`).
5. Enqueue `{"Value":"s23"}` and confirm the job executes — schema is not only created but
   functional.
**Expected:** after a complete wipe, startup migrations recreate the full schema and the app
   works normally.

### S24. Tracing modes
The sample app instruments incoming HTTP requests with OpenTelemetry
(`AddAspNetCoreInstrumentation`), so every `POST` runs inside a request `Activity` whose
TraceId appears in the console exporter output when the request completes. Job creation
inside a request can capture the full W3C traceparent (TraceId + SpanId + flags) of that
Activity (`traceparent` column of `jobby_jobs`, mode in `tracing_mode`: 0 = CreateNewTraceId,
1 = RestoreTraceId, 2 = LinkTraceId) and the execution either continues the trace
(RestoreTraceId: job span is a child of the request span) or links to it (LinkTraceId).

Practical notes (verified in the first run):
- SPEED: `DemoJobCommand` already carries `[JsonConverter(typeof(JsonStringEnumConverter))]`
  on `TracingMode`, so string enum values in JSON bodies work out of the box — this scenario
  needs NO code edits, only appsettings.json changes. `dotnet run` serves appsettings.json
  from the project dir, so a config edit needs only a process restart, NEVER a rebuild.
  Total scenario cost is one build (shared with the copy setup) and one mid-scenario restart.
- SPEED: `DelayMs=1500` is enough. The DB row is visible the whole time before completion,
  so query the DB IMMEDIATELY after the POST (poll until the row appears, ~1 s — it is in
  status 1 or 2 then), do not wait for the execution to check traceparent/tracing_mode.
  Then poll the log for `Executing demo job, value is s24x` (executes within ~1–2 s at the
  default 500 ms polling). NOTE: the handler line means the span STARTED, not finished —
  the console exporter prints the execution Activity block only after the span STOPS, so
  wait out the remaining `DelayMs` (~2 s with `DelayMs=1500`) after that line before
  snapshotting the log and reading Activity blocks (bit in practice: a snapshot taken right
  after the handler line contained no execution block yet).
- The console exporter prints the request span AFTER the HTTP response returns, so for a
  `DelayMs` job the job execution block appears BEFORE its request-span block in the log.
  Correlate by the TraceId hex string, not by order. Identify blocks by the line after
  `Activity.TraceId`/`SpanId` — `Activity.DisplayName` (`POST jobs/enqueue-job` vs
  the job name, e.g. `DemoJob` — the execution span is named after the job). A delay-free
  job's request span is exported immediately though; either way, matching by hex is the
  reliable method.
- When extracting a request span's TraceId by its `Activity.DisplayName`, grep with a WIDE
  look-back window (`grep -B9`, not `-B7`): the number of lines between `Activity.TraceId`
  and `DisplayName` varies (a `ParentSpanId` line may or may not be present, e.g. when curl
  sends no traceparent header vs propagation middleware), so a tight window can miss the
  right line (bit in practice).
- The console exporter DOES print links: a LinkTraceId execution shows an `Activity.Links:`
  section with one `<TraceId> <SpanId>` line per link — matching it against the DB
  `traceparent` is the direct evidence for step 4.
- The request span's `SpanId` is printed in its Activity block — needed to verify
  `ParentSpanId` of the job execution span in RestoreTraceId mode (they must be equal now,
  the full traceparent is restored) and the link's `SpanId` in LinkTraceId mode.
- The stored traceparent flags are always `-01` here: the app does not configure a sampler,
  so the default ASP.NET instrumentation records every local request. The unsampled-parent
  path (`-00`, job span stays unrecorded) is covered by unit tests, not by this scenario.
- The instance log is written CONCURRENTLY with your checks: counts taken at different
  moments on the same file differ (bit in practice — a recurrent job executes every 5 s
  while you grep). `cp` the log to a temp snapshot and run all counting/extracting (awk,
  uniq) against the snapshot, not the live file.
- If you truncated `jobby_jobs` after instance startup (as this scenario's restarts may
  prompt), the startup `EmptyRecurrentJob` will NOT come back by itself — before step 6
  schedule it manually: `POST /jobs/schedule-recurrent` with `"*/5 * * * * *"`.
- `DemoJobCommand` accepts `"TracingMode": "RestoreTraceId" | "LinkTraceId" | "CreateNewTraceId"`
  (omit → global setting); `appsettings.json` binds the same names for the global
  `Jobby:TracingMode` value.

1. Edit the copy's `appsettings.json`: `"Jobby": { "UseTracing": true, "TracingMode": "RestoreTraceId" }`
   (keep `UseMetrics: true`). Start instance 1 (restart only — appsettings changes need
   no rebuild).
2. Default inherit (global RestoreTraceId): `POST /jobs/enqueue-job` with
   `{"Value":"s24a","DelayMs":1500}`. Immediately poll the DB
   until the row appears (status 1 or 2 — do not wait for the execution) and check:
   - DB: the new `DemoJob` row has `traceparent` = `00-<request TraceId>-<request SpanId>-01`
     and `tracing_mode` = 1;
   then poll the log for `Executing demo job, value is s24a` and check the console output:
   the job's execution Activity (`JobbyActivitySourceNames.JobsExecution`)
   has the SAME TraceId as the request span AND its `Activity.ParentSpanId` equals the
   request span's SpanId (true child of the creating span). Also verify the execution
   span is identified by the job name (`Activity.DisplayName: DemoJob`).
3. Per-job override (off): `POST /jobs/enqueue-job` with
   `{"Value":"s24b","DelayMs":1500,"TracingMode":"CreateNewTraceId"}`.
   DB (immediately): `traceparent` NULL, `tracing_mode` = 0; the execution Activity has a
   TraceId different from the request's.
4. LinkTraceId: `POST /jobs/enqueue-job` with
   `{"Value":"s24c","DelayMs":1500,"TracingMode":"LinkTraceId"}`.
   DB (immediately): `traceparent` is the full request traceparent, `tracing_mode` = 2;
   console: the execution Activity has a NEW TraceId (different from the request's), no
   parent, and its `Activity.Links` contain the request trace (the link's TraceId/SpanId
   equal the request span's).
5. Failed job is traced as an error: `POST /jobs/enqueue-job` with
   `{"Value":"s24fail","ShouldBeFailed":true}` (no DelayMs). Poll the log until the retry
   policy is exhausted (3 attempts, `failed` LogError on the last). Every execution attempt
   exports a `DemoJob` Activity; the FAILED attempt's block has `Activity.StatusCode: Error`
   with the exception message and an `exception` event containing
   `exception.type`/`exception.message`/`exception.stacktrace`. DB (final row, it is not
   deleted on failure): `status=4`.
6. Recurrent jobs never capture tracing context: with the same global config, check the
   `EmptyRecurrentJob` row has `traceparent` NULL and `tracing_mode` = 0 (schedule it
   first if the table was truncated) and its execution Activities keep starting fresh
   traces.
7. Per-job override (on, global off): edit `appsettings.json` to
   `"UseTracing": true, "TracingMode": "CreateNewTraceId"`, restart instance 1 (no rebuild).
   `POST /jobs/enqueue-job` with `{"Value":"s24d","DelayMs":1500,"TracingMode":"RestoreTraceId"}`.
   DB (immediately): `traceparent` equals the request traceparent, `tracing_mode` = 1; the
   execution Activity continues it (same TraceId, ParentSpanId = request SpanId).
8. Restore `appsettings.json` to `"UseTracing": false`.
**Expected:** each job execution produces an Activity in stdout, named after the job; the
   full traceparent survives the DB roundtrip; in RestoreTraceId mode the execution is a
   child span of the creating request span; per-job `JobOpts.TracingMode` overrides the
   global setting in both directions; LinkTraceId produces an independent trace with a
   link; recurrent jobs always start new traces; failed jobs traced as errors.

### S25. Bulk load: thousands of jobs drain without stalls (run twice)
Run this scenario twice: once with default `CompleteWithBatching=false` and once with
`CompleteWithBatching=true` (edit the copy's `UseServerSettings` in `Program.cs` — a code
edit, so kill→edit→build→start between runs; the appsettings no-rebuild shortcut does not apply).
Default config otherwise (polling 500 ms, retry 3×[1,2] s, queues `default` + `heavy`).

1. TRUNCATE `jobby_jobs`. Start BOTH instances (:5075 and :5076). Then remove the startup
   recurrent job before doing anything else: on startup each instance schedules an exclusive
   `EmptyRecurrentJob` (cron `*/5`), which keeps a permanent row in `jobby_jobs` forever and
   makes the whole-table drain check in step 4/6 unreachable.
   `POST /jobs/cancel-recurrent` to one instance (the job is exclusive and shared via the DB,
   so one call is enough), then verify `SELECT count(*) FROM jobby_jobs WHERE
   job_name='EmptyRecurrentJob'` returns 0. Instances do NOT re-schedule the recurrent job
   after startup, so the table stays clean for the rest of the run.
2. Create ~2000 jobs via 20 calls to `/jobs/enqueue-batch`, 100 commands per call,
   alternating requests between the two instances. Each batch of 100:
   - 80 jobs WITHOUT `SerializableGroupId` (mix queues: ~50 to `default`, ~30 to `heavy`),
   - 20 jobs across 10 serializable groups (2 jobs per group per batch, e.g.
     `s25-g0` … `s25-g9`; vary group membership between batches so groups interleave).
   Use `{"Value":"s25-run1-N-…", "DelayMs":50}` style values (unique per batch) so executions
   are countable in logs; no `ShouldBeFailed` — all jobs must succeed.
3. Collect ALL returned job ids from the 20 responses (must total 2000).
4. Poll the DB every ~5 s (not just once at the timeout):
   `SELECT count(*) FROM jobby_jobs` — with the startup recurrent job cancelled in step 1
   this whole-table count must reach 0; any leftover row is a stuck test job. (The id-scoped
   check `WHERE id IN (<all ids>)` gives the same result and can be used as a fallback.)
5. Also monitor progress via the logs: count `Executing demo job, value is s25-run…` lines
   in BOTH instance logs; their sum must eventually reach 2000 and both instances must have
   executed a share of the jobs.
6. Success criteria (before the 120 s timeout):
   - the DB count of our ids reaches 0 (with `DeleteCompleted=true` this means every job
     completed — none left `Scheduled`/`Processing`/`Failed`/`Frozen`/`WaitingPrev`); since
     the startup recurrent job was cancelled in step 1, the whole `jobby_jobs` table must be
     empty at the end;
   - total executions across both logs = 2000;
   - both instances participated;
   - no error spam in the logs (retry-policy warnings are NOT expected here at all).
   If the count does not reach 0, diagnose by status: rows stuck in `WaitingPrev` point to a
   broken chain, `Frozen` — to group freezing, `Processing` with a live `server_id` — to a hang.
7. TRUNCATE `jobby_jobs` between the two runs; after the second run restore
   `CompleteWithBatching=false` if you plan to keep the copy (the copy is deleted in cleanup
   anyway).
**Expected:** 2000 mixed jobs (plain + grouped, two queues, two servers) fully drain well
   under the 2-minute timeout in both `CompleteWithBatching` modes, with both instances
   working and zero stuck rows.

### S26. Recurrent jobs with interval and custom scheduler
1. `POST /jobs/schedule-recurrent-with-interval` with body `2` — schedules the exclusive
   `EmptyRecurrentJobCommand` with a built-in `TimeSpanSchedule` (interval seconds).
2. Log: `EmptyRecurrentJob` executions every ~2 s. DB: ONE `EmptyRecurrentJob` row,
   `schedule` NOT a cron string (interval-based), `queue_name='recurrent'`.
   NOTE: the FIRST execution after re-scheduling over the startup cron job may still
   follow the old 5 s cadence (the next-run timestamp was already computed) — judge the
   new cadence starting from the second execution.
3. `POST /jobs/schedule-recurrent-with-custom-scheduler` with body `1` — schedules
   `CustomSchedulerRecurrentJobCommand` via the custom `SecondsIntervalSchedule`
   (resolved by the registered `SecondsIntervalScheduleHandler`).
4. Log: `CustomSchedulerRecurrentJob` executions every ~1 s. DB: a separate
   `CustomSchedulerRecurrentJob` row appears next to the `EmptyRecurrentJob` row
   (different job names do not collide). Both rows have `queue_name='recurrent'`
   (`UseQueueForAllRecurrent` routes every recurrent job there, including the
   custom-scheduler one).
5. `POST /jobs/cancel-recurrent-with-custom-scheduler`: the `CustomSchedulerRecurrentJob`
   row disappears / executions stop, while `EmptyRecurrentJob` keeps running every ~2 s.
6. `POST /jobs/cancel-recurrent`: the `EmptyRecurrentJob` row disappears / executions stop.
**Expected:** both non-cron schedule types execute on their interval, are stored as
   separate rows, and each is cancelled by its own endpoint without touching the other.

## 7. Report

After all scenarios, output a summary table:

| Scenario | Verdict | Evidence / notes |
|---|---|---|

Mark each PASS/FAIL/SKIPPED (with reason). For any FAIL include the exact expected-vs-actual
and relevant log excerpt / DB rows. Report overall suite status.

## 8. Cleanup (mandatory, even on partial failure)

1. Stop all app instances (kill dotnet processes you started).
2. Truncate all three tables (`jobby_jobs`, `jobby_servers`, `jobby_unlocking_groups`).
3. Delete the disposable copy: `rm -rf temp/Jobby.Samples.AspNet`
   (and the `temp/regression/logs/` directory if the user doesn't need it).
4. Verify the repo is untouched: `git status` must show no modifications outside
   `temp/` (the pre-existing changes the user had before the run are not yours to revert).
5. Leave the docker container running (the user may need it) — do not stop postgres.

## Troubleshooting

- App can't connect to DB (`Connection refused`): check `pg_isready` in WSL; if WSL2 port
  forwarding is broken, restart the container or ask the user.
- `docker compose` (plugin) not found: use `docker-compose` (v1) — that's the installed one.
- Two app instances share the DB — always check `server_id` when attributing log lines to an instance.
- If a scenario needs a new command field, edit **the copy's** code only (see S17 pattern).
