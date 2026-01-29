ALTER TABLE ${jobs_table_fullname}
    ADD COLUMN IF NOT EXISTS queue_name TEXT NOT NULL DEFAULT 'default';

CREATE INDEX IF NOT EXISTS ${tables_prefix}
    ON ${jobs_table_fullname}(queue_name, status, scheduled_start_at);

DROP INDEX IF EXISTS ${tables_prefix}jobs_status_scheduled_start_at_idx;