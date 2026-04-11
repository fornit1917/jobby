CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_frozen_group_id_scheduled_start_at_idx
    ON ${jobs_table_fullname}(serializable_group_id, scheduled_start_at)
    WHERE serializable_group_id IS NOT NULL AND status = 6;

CREATE TABLE IF NOT EXISTS ${unlocking_groups_table_fullname} (
    group_id TEXT NOT NULL PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL 
);

CREATE INDEX IF NOT EXISTS ${tables_prefix}unlocking_groups_created_at_idx
    ON ${unlocking_groups_table_fullname}(created_at); 