-- Tables

CREATE TABLE IF NOT EXISTS jobby_jobs (
	id UUID NOT NULL PRIMARY KEY,
	job_name TEXT NOT NULL,
	cron TEXT DEFAULT NULL,
	job_param TEXT DEFAULT NULL,
	status int NOT NULL,
	error TEXT DEFAULT NULL,
	created_at timestamptz NOT NULL,
	scheduled_start_at timestamptz NOT NULL,
	last_started_at timestamptz DEFAULT NULL,
	last_finished_at timestamptz DEFAULT NULL,
	started_count int NOT NULL DEFAULT 0,
	next_job_id UUID DEFAULT NULL,
	server_id TEXT DEFAULT NULL,
	can_be_restarted boolean NOT NULL DEFAULT FALSE,
    queue_name TEXT NOT NULL DEFAULT 'default',
    serializable_group_id TEXT DEFAULT NULL,
    lock_group_if_failed BOOLEAN DEFAULT FALSE,
    is_group_locker BOOLEAN GENERATED ALWAYS AS (
        serializable_group_id IS NOT NULL
        AND (
            status = 2
            OR (
                lock_group_if_failed = TRUE
                AND (status = 4 OR status = 1 AND started_count > 0)
            )
        )
    ) STORED,
    is_exclusive BOOLEAN NOT NULL DEFAULT FALSE,
    scheduler_type TEXT DEFAULT NULL
);

CREATE INDEX IF NOT EXISTS jobby_jobs_queue_name_status_scheduled_start_at_idx
    ON jobby_jobs(queue_name, status, scheduled_start_at);

CREATE UNIQUE INDEX IF NOT EXISTS jobby_jobs_exclusive_name_idx
    ON jobby_jobs(job_name)
    WHERE is_exclusive = true;

CREATE UNIQUE INDEX IF NOT EXISTS jobby_jobs_locked_group_idx
    ON jobby_jobs(serializable_group_id)
    WHERE is_group_locker = TRUE;

CREATE TABLE IF NOT EXISTS jobby_servers (
	id TEXT NOT NULL PRIMARY KEY,
	heartbeat_ts timestamptz NOT NULL
);

-- Functions

CREATE OR REPLACE FUNCTION jobby_take_to_processing(p_queue TEXT, p_batch_size int, p_server_id TEXT)
RETURNS TABLE (
    id uuid,
    job_name TEXT,
    job_param TEXT,
    started_count int,
    cron TEXT,
    next_job_id uuid,
    scheduled_start_at timestamptz,
    server_id TEXT,
    scheduler_type TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    rec record;
    batch_size int;
    taken_count int;
    skipped_count int;
    max_iterations_count int;
    iterations_count int;
BEGIN
    max_iterations_count := 10;
    iterations_count := 0;
    batch_size = p_batch_size;
    LOOP
        taken_count := 0;
        skipped_count := 0;
        iterations_count := iterations_count + 1;
        FOR rec IN
            WITH
                candidates AS (
                    SELECT c.id, c.serializable_group_id, c.scheduled_start_at FROM jobby_jobs c
                    WHERE
                        c.queue_name = p_queue
                        AND c.status = 1
                        AND c.scheduled_start_at <= now()
                    AND (
                        c.serializable_group_id IS NULL
                        OR (
                            NOT EXISTS (
                                SELECT 1 FROM jobby_jobs jl
                                WHERE
                                    jl.serializable_group_id = c.serializable_group_id
                                    AND jl.is_group_locker = TRUE
                                    AND jl.id != c.id
                            ) 
                            AND pg_try_advisory_xact_lock(hashtext(c.serializable_group_id)) = TRUE                                
                        )
                    )
                    ORDER BY scheduled_start_at
                    LIMIT batch_size
                    FOR UPDATE SKIP LOCKED
                ),
                candidates_without_group AS (
                    SELECT c.id FROM candidates c WHERE serializable_group_id IS NULL
                ),
                candidates_with_group as (
                    SELECT DISTINCT ON(serializable_group_id) c.id, c.serializable_group_id 
                    FROM candidates c
                    WHERE
                        c.serializable_group_id IS NOT NULL
                        ORDER BY c.serializable_group_id, c.scheduled_start_at 
                    ),
                taken AS (
                    UPDATE jobby_jobs t
                    SET
                        status = 2,
                        last_started_at = now(),
                        started_count = t.started_count + 1,
                        server_id = p_server_id
                    WHERE t.id IN (
                        SELECT c1.id FROM candidates_without_group c1
                        UNION 
                        SELECT c2.id FROM candidates_with_group c2
                    )
                    RETURNING 
                        TRUE as r_taken,
                        t.id AS r_id,
                        t.job_name AS r_job_name,
                        t.job_param AS r_job_param,
                        t.started_count AS r_started_count,
                        t.cron AS r_cron,
                        t.next_job_id AS r_next_job_id,
                        t.scheduled_start_at AS r_scheduled_start_at,
                        t.server_id AS r_server_id,
                        t.scheduler_type AS r_scheduler_type
                ),
                skipped AS (
                    SELECT
                        FALSE as r_taken,
                        c.id AS r_id,
                        NULL AS r_job_name,
                        NULL AS r_job_parama,
                        0 AS r_started_count,
                        NULL AS r_cron,
                        NULL::uuid AS r_next_job_id,
                        NULL::timestamptz AS r_scheduled_start_at,
                        NULL AS r_server_id,
                        NULL AS r_scheduler_type
                    FROM candidates c 
                    WHERE c.serializable_group_id IS NOT NULL 
                    AND c.id NOT IN (
                        SELECT cwg.id FROM candidates_with_group cwg
                    )
                )
                SELECT * FROM taken
                UNION
                SELECT * FROM skipped
            LOOP
                IF rec.r_taken = TRUE THEN
                    id := rec.r_id;
                    job_name := rec.r_job_name;
                    job_param := rec.r_job_param;
                    started_count := rec.r_started_count;
                    cron := rec.r_cron;
                    next_job_id := rec.r_next_job_id;
                    scheduled_start_at := rec.r_scheduled_start_at;
                    server_id := rec.r_server_id;
                    scheduler_type := rec.r_scheduler_type;
                    
                    RETURN NEXT;
                    
                    taken_count := taken_count + 1;
                ELSE
                    skipped_count := skipped_count + 1;
                END IF;
            END LOOP;
  
        IF taken_count = 0 AND skipped_count = 0 
            OR skipped_count = 0 
            OR iterations_count >= max_iterations_count THEN
                EXIT;
        ELSE
            batch_size := skipped_count;
        END IF;
    END LOOP;  
END;
$$