using UnityEngine;
using System;
using System.IO;
using System.Text;

public class Record6DOF5ms : MonoBehaviour
{
    [SerializeField]
    private GameObject targetObject;

    private StreamWriter writer;
    private string filePath;
    private float nextRecordTime = 0f;
    private const float RECORD_INTERVAL = 0.005f;

    private void Start()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // Use project-root Logs/streaming folder (project root is parent of Assets)
        string baseLogsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "streaming"));
        filePath = Path.Combine(baseLogsDir, $"6dof_{timestamp}.csv");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

        writer = new StreamWriter(filePath, true, Encoding.UTF8);
        writer.WriteLine("timestamp,x,y,z,qx,qy,qz,qw");

        Debug.Log("紀錄檔 6DOF 檔案: " + filePath);
    }

    private void Update()
    {
        if (targetObject != null && Time.time >= nextRecordTime)
        {
            Vector3 position = targetObject.transform.position;
            Quaternion rotation = targetObject.transform.rotation;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string data = $"{timestamp},{position.x:F4},{position.y:F4},{position.z:F4},{rotation.x:F4},{rotation.y:F4},{rotation.z:F4},{rotation.w:F4}";

            writer.WriteLine(data);
            writer.Flush();

            nextRecordTime = Time.time + RECORD_INTERVAL;

            if (Application.isEditor)
            {
                Debug.Log($"紀錄 6DOF 資料: {data}");
            }
        }
    }

    private void OnDestroy()
    {
        // 關閉檔案
        if (writer != null)
        {
            writer.Close();
            Debug.Log("6DOF 紀錄檔已關閉");
        }
    }
}