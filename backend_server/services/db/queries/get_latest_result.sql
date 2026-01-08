-- migrations/0004_get_latest_result.sql
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
WHERE task_id = @task_id
ORDER BY created_at DESC, id DESC
LIMIT 1;