ALTER TABLE ${jobs_table_fullname}
    ADD COLUMN IF NOT EXISTS is_exclusive BOOLEAN NOT NULL DEFAULT FALSE;

UPDATE ${jobs_table_fullname} SET is_exclusive = true WHERE cron IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ${tables_prefix}jobs_exclusive_name_idx 
    ON ${jobs_table_fullname}(job_name)
    WHERE is_exclusive = true;

DROP INDEX IF EXISTS ${tables_prefix}jobs_recurrent_name_idx;
