CREATE OR REPLACE FUNCTION ${take_to_processing_function_fullname}(p_queue TEXT, p_batch_size int, p_server_id TEXT)
RETURNS TABLE (
    id uuid,
    job_name TEXT,
    job_param TEXT,
    started_count int,
    schedule TEXT,
    next_job_id uuid,
    scheduled_start_at timestamptz,
    server_id TEXT,
    scheduler_type TEXT
)
LANGUAGE plpgsql
AS $$
DECLARE
    result_rec record;
    candidate_rec record;
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
        
        FOR candidate_rec IN
            WITH
                candidates AS (
                    SELECT c.id, c.serializable_group_id, c.scheduled_start_at FROM ${jobs_table_fullname} c
                    WHERE
                        c.queue_name = p_queue
                        AND c.status = 1
                        AND c.scheduled_start_at <= now()
                        AND (
                            c.serializable_group_id IS NULL
                            OR (
                                NOT EXISTS (
                                    SELECT 1 FROM ${jobs_table_fullname} jl
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
                )
            SELECT 
                FALSE AS c_skipped,
                c1.id AS c_id,
                NULL AS c_serializable_group_id 
            FROM candidates_without_group c1
            
            UNION
            
            SELECT
                FALSE AS c_skipped,
                c2.id AS c_id, 
                c2.serializable_group_id AS c_serializable_group_id 
            FROM candidates_with_group c2
            
            UNION
            
            SELECT
                TRUE AS c_skipped,
                c3.id AS c_id,
                c3.serializable_group_id AS c_serializable_group_id
            FROM candidates c3
            WHERE 
                c3.serializable_group_id IS NOT NULL 
                AND c3.id NOT IN (
                    SELECT cwg.id FROM candidates_with_group cwg
                )        

            LOOP
                IF candidate_rec.c_skipped = FALSE THEN
                    UPDATE ${jobs_table_fullname} t
                    SET
                        status = 2,
                        last_started_at = now(),
                        started_count = t.started_count + 1,
                        server_id = p_server_id
                    WHERE
                        t.id = candidate_rec.c_id
                        AND NOT EXISTS (
                            SELECT 1 FROM ${jobs_table_fullname} jl
                            WHERE
                                jl.serializable_group_id = candidate_rec.c_serializable_group_id
                                AND jl.is_group_locker = TRUE
                        )
                    RETURNING
                        t.id AS r_id,
                        t.job_name AS r_job_name,
                        t.job_param AS r_job_param,
                        t.started_count AS r_started_count,
                        t.schedule AS r_schedule,
                        t.next_job_id AS r_next_job_id,
                        t.scheduled_start_at AS r_scheduled_start_at,
                        t.server_id AS r_server_id,
                        t.scheduler_type AS r_scheduler_type
                    INTO result_rec;    
                
                    IF FOUND THEN
                        id := result_rec.r_id;
                        job_name := result_rec.r_job_name;
                        job_param := result_rec.r_job_param;
                        started_count := result_rec.r_started_count;
                        schedule := result_rec.r_schedule;
                        next_job_id := result_rec.r_next_job_id;
                        scheduled_start_at := result_rec.r_scheduled_start_at;
                        server_id := result_rec.r_server_id;
                        scheduler_type := result_rec.r_scheduler_type;                        
                        RETURN NEXT;
                    
                        taken_count := taken_count + 1;
                    ELSE
                        skipped_count := skipped_count + 1;
                    END IF;
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
