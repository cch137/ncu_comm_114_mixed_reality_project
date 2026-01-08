-- migrations/0003_get_result.sql
SELECT
  id,
  task_id,
  version,
  code,
  error,
  mime_type,
  blob_content,
  created_at
FROM object_generation_results
WHERE task_id = @task_id AND version = @version
LIMIT 1;