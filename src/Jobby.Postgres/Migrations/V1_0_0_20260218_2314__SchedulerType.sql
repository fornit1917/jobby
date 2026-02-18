ALTER TABLE ${jobs_table_fullname}
    ADD COLUMN IF NOT EXISTS scheduler_type TEXT DEFAULT NULL;