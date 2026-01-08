-- Upsert task first, then upsert result (unique: task_id + version)
WITH _task AS (
  INSERT INTO object_generation_tasks (id, name, description)
  VALUES (@task_id, @task_name, @task_description)
  ON CONFLICT(id) DO UPDATE SET
    name = excluded.name,
    description = excluded.description
  RETURNING id
)
INSERT INTO object_generation_results (
  task_id,
  version,
  code,
  error,
  mime_type,
  blob_content
)
VALUES (
  @task_id,
  @version,
  @code,
  @error,
  @mime_type,
  @blob_content
)
ON CONFLICT(task_id, version) DO UPDATE SET
  code = excluded.code,
  error = excluded.error,
  mime_type = excluded.mime_type,
  blob_content = excluded.blob_content
RETURNING id;