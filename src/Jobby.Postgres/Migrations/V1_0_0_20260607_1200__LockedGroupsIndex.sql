CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_serializable_group_status_scheduled_start_at_idx
    ON ${jobs_table_fullname}(serializable_group_id, status, scheduled_start_at)
    WHERE serializable_group_id IS NOT NULL AND schedule IS NULL;
