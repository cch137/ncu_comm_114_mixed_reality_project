using UnityEngine;
using System;
using System.IO;
using System.Text;

public class Record6DOF5ms : MonoBehaviour
{
    [SerializeField]
    private GameObject targetObject;  //nOؼЪ

    private StreamWriter writer;
    private string filePath;
    private float nextRecordTime = 0f;
    private const float RECORD_INTERVAL = 0.05f;  // 5@

    private void Start()
    {
        // build a path under project root: <project>/Logs/streaming/6dof_*.csv
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // Application.dataPath points to <project>/Assets, so project root is one level up
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string logsDir = Path.Combine(projectRoot, "Logs", "streaming");
        filePath = Path.Combine(logsDir, $"6dof_{timestamp}.csv");

        // Ensure the directory exists under project root
        Directory.CreateDirectory(logsDir);

        // Open writer for the new location
        writer = new StreamWriter(filePath, true, Encoding.UTF8);
        writer.WriteLine("timestamp,x,y,z,qx,qy,qz,qw");

        Debug.Log($"lO 6DOF ƨ: {filePath}");
    }

    private void Update()
    {
        if (targetObject != null && Time.time >= nextRecordTime)
        {
            // mM|
            Vector3 position = targetObject.transform.position;
            Quaternion rotation = targetObject.transform.rotation;

            //O
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string data = $"{timestamp}," +
                         $"{position.x:F4},{position.y:F4},{position.z:F4}," +
                         $"{rotation.x:F4},{rotation.y:F4},{rotation.z:F4},{rotation.w:F4}";

            writer.WriteLine(data);
            writer.Flush();  //TOƥߧYgJɮ

            //sUOɶ
            nextRecordTime = Time.time + RECORD_INTERVAL;

            //bs边ܧYɸ
            if (Application.isEditor)
            {
                Debug.Log($"O 6DOF : {data}");
            }
        }
    }

    private void OnDestroy()
    {
        // ɮ
        if (writer != null)
        {
            writer.Close();
            Debug.Log("6DOFOw");
        }
    }
}