CREATE TABLE IF NOT EXISTS jobby_jobs (
	id UUID NOT NULL PRIMARY KEY,
	job_name TEXT NOT NULL,
	cron TEXT DEFAULT NULL,
	job_param TEXT DEFAULT NULL,
	status int NOT NULL,
	created_at timestamptz NOT NULL,
	scheduled_start_at timestamptz NOT NULL,
	last_started_at timestamptz DEFAULT NULL,
	last_finished_at timestamptz DEFAULT NULL,
	started_count int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS jobby_jobs_status_scheduled_start_at_idx ON jobby_jobs(status, scheduled_start_at);
CREATE UNIQUE INDEX IF NOT EXISTS jobby_jobs_recurrent_name_idx ON jobby_jobs(job_name) WHERE cron IS NOT NULL;