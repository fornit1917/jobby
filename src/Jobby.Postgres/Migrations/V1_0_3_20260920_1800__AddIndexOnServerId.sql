CREATE INDEX IF NOT EXISTS ${tables_prefix}jobs_server_id_idx 
    ON ${jobs_table_fullname}(server_id) 
    WHERE status = 2;
