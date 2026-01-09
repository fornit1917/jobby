CREATE TABLE IF NOT EXISTS ${jobs_table_fullname} (
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
	sequence_id TEXT DEFAULT NULL
);

CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_status_scheduled_start_at_idx ON ${jobs_table_fullname}(status, scheduled_start_at);
CREATE UNIQUE INDEX IF NOT EXISTS ${tables_prefix}jobs_recurrent_name_idx ON ${jobs_table_fullname}(job_name) WHERE cron IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ${tables_prefix}jobs_uniq_sequence_id_idx ON ${jobs_table_fullname}(sequence_id) WHERE status = 1 OR status = 2;
CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_sequence_id_idx ON ${jobs_table_fullname}(sequence_id) WHERE sequence_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS ${servers_table_fullname} (
	id TEXT NOT NULL PRIMARY KEY,
	heartbeat_ts timestamptz NOT NULL
);