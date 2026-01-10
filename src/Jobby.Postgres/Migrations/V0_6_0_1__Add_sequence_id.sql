ALTER TABLE ${jobs_table_fullname}
	ADD COLUMN IF NOT EXISTS sequence_id TEXT DEFAULT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ${tables_prefix}jobs_uniq_sequence_id_idx ON ${jobs_table_fullname}(sequence_id) WHERE status = 1 OR status = 2;
CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_sequence_id_idx ON ${jobs_table_fullname}(sequence_id, scheduled_start_at) WHERE sequence_id IS NOT NULL;
