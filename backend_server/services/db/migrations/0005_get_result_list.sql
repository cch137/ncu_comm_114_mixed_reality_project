-- migrations/0005_get_result_list.sql
SELECT
  version,
  (
    code IS NOT NULL
    AND mime_type IS NOT NULL
    AND blob_content IS NOT NULL
  ) AS success,
  error
FROM object_generation_results
WHERE task_id = @task_id
ORDER BY created_at DESC, id DESC;