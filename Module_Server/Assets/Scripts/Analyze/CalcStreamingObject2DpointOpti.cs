using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;

public class Caculate_Reprojection_2D
{
    public float Object_Screen_norm_x { get; set; }
    public float Object_Screen_norm_y { get; set; }
    public float Object_ScreenPosition_z { get; set; }
    public float Object_OriginalPosition_x { get; set; }
    public float Object_OriginalPosition_y { get; set; }
    public float Object_OriginalPosition_z { get; set; }
    public string CameraName { get; set; }
    public string ObjectName { get; set; }
}

public class CameraErrorStats
{
    public string cameraName;
    public string objectName;
    public float totalError;
    public int frameCount;
    public float averageError;
    public List<float> errorHistory;
    public DateTime lastUpdateTime;
    public float currentLatency;
    public float averageLatency;
    public float totalLatency;
    public List<float> latencyHistory;
    public float currentBitrate;
}

public class CalcStreamingObject2DpointOpti : MonoBehaviour
{
    [SerializeField]
    PeerConnection PeerConnection;

    [SerializeField]
    private string expMethod = "EXP";

    private List<GameObject> meshPositions = new List<GameObject>();
    [SerializeField]
    private List<Camera> streamingCams = new List<Camera>();

    private string json_reporjection_data_2D;
    private Vector2 originalsize;
    private Dictionary<string, CameraErrorStats> errorStats = new Dictionary<string, CameraErrorStats>();
    private string logFilePath;
    private StreamWriter logWriter;
    private bool isLogging = false;
    private UpdateGameObjectFromCSV userPositionController;
    private List<VolumetricUpdate.ClusterInfo> clusters = VolumetricUpdate.GetClusterInfos();

    private void Start()
    {
        GameObject objectManager = GameObject.Find("ObjectManager");
        if (objectManager != null)
        {
            for (int i = 1; i <= 20; i++)
            {
                string objectName = $"Object{i}";
                Transform objectTransform = objectManager.transform.Find(objectName);
                if (objectTransform != null && objectTransform.childCount > 0)
                {
                    meshPositions.Add(objectTransform.GetChild(0).gameObject);
                    Debug.Log($"找到並添加物件: {objectName}");
                }
            }
        }

        userPositionController = FindObjectOfType<UpdateGameObjectFromCSV>();
        if (userPositionController != null)
        {
            Debug.Log($"找到 UpdateGameObjectFromCSV，當前 dataType: {userPositionController.dataType}");
        }
        else
        {
            Debug.LogError("找不到 UpdateGameObjectFromCSV 組件");
        }
        InitializeErrorLogging();
    }

    private void OnDestroy()
    {
        StopErrorLogging();
    }

    private void InitializeErrorLogging()
    {
        string baseLogsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "streaming"));
        string dofType = userPositionController != null ?
            GetDofTypeName(userPositionController.dataType) : "固定";

        Debug.Log($"創建記錄目錄，使用 6DOF 類型: {dofType}");

        string logsDirectory = Path.Combine(baseLogsDirectory, expMethod, dofType);

        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
            Debug.Log($"創建誤差記錄目錄: {logsDirectory}");
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(logsDirectory, $"camera_errors_{timestamp}.csv");

        try
        {
            logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8);
            logWriter.WriteLine("時間,相機名稱,物件名稱,當前誤差,平均誤差,總誤差,層數,當前延遲,平均延遲,總延遲");
            isLogging = true;
            Debug.Log($"開始記錄相機誤差到檔案: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"無法創建誤差記錄檔案: {e.Message}");
        }
    }

    private string GetDofTypeName(UserPositionDataType type)
    {
        switch (type)
        {
            case UserPositionDataType.固定:
                return "固定";
            case UserPositionDataType.走動:
                return "走動";
            case UserPositionDataType.走動後回原地:
                return "走動後回原地";
            default:
                return "固定";
        }
    }

    private void StopErrorLogging()
    {
        if (logWriter != null)
        {
            try
            {
                logWriter.Flush();
                logWriter.Close();
                isLogging = false;
                Debug.Log($"相機誤差記錄已停止，檔案位置: {logFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"關閉誤差記錄檔案時發生錯誤: {e.Message}");
            }
        }
    }

    private void Update()
    {
        UpdateStreamingCamsByCluster();

        int count = Mathf.Min(meshPositions.Count, streamingCams.Count);

        for (int i = 0; i < count; i++)
        {
            json_reporjection_data_2D = Datatobetransmitted(meshPositions[i], streamingCams[i]);
            if (PeerConnection == null)
            {
                Debug.LogWarning($"CalcStreamingObject2DpointOpti.Update: PeerConnection is not assigned. Skipping data channel send for {meshPositions[i].name}.");
            }
            else
            {
                try
                {
                    PeerConnection.UseDataChannel("SendtoClient", json_reporjection_data_2D);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"CalcStreamingObject2DpointOpti.Update: Exception while sending data channel: {ex}");
                }
            }
        }

        CalculateAndLogError();
    }

    private void UpdateStreamingCamsByCluster()
    {
        Debug.Log($"目前 clusters 數量: {clusters.Count}");
        foreach (var cluster in clusters)
        {
            Debug.Log($"群集 {cluster.clusterId} 主相機: {cluster.mainCamera?.name ?? "無"}，物件: {string.Join(", ", cluster.objects.Select(o => o.name))}");
        }

        Dictionary<string, string> objectToMainCamera = new Dictionary<string, string>();
        foreach (var cluster in clusters)
        {
            if (cluster.mainCamera != null)
            {
                foreach (var obj in cluster.objects)
                {
                    objectToMainCamera[obj.name] = cluster.mainCamera.name;
                }
            }
        }

        for (int i = 0; i < meshPositions.Count; i++)
        {
            string objName = meshPositions[i].name;
            if (objectToMainCamera.ContainsKey(objName))
            {
                string mainCameraObjName = objectToMainCamera[objName];
                string renderCamName = $"Render_{mainCameraObjName}";
                GameObject renderCamObj = GameObject.Find(renderCamName);
                if (renderCamObj != null)
                {
                    Camera cam = renderCamObj.GetComponent<Camera>();
                    if (cam != null)
                    {
                        if (i < streamingCams.Count)
                            streamingCams[i] = cam;
                        else
                            streamingCams.Add(cam);
                        Debug.Log($"streamingCams[{i}] 設為 {cam.name} (Render_{mainCameraObjName})，對應物件 {objName}");
                    }
                }
            }
        }
    }

    private void CalculateAndLogError()
    {
        int count = Mathf.Min(meshPositions.Count, streamingCams.Count);

        for (int i = 0; i < count; i++)
        {
            GameObject meshPosition = meshPositions[i];
            Camera camera = streamingCams[i];

            string key = $"{camera.name}_{meshPosition.name}";
            if (!errorStats.ContainsKey(key))
            {
                errorStats[key] = new CameraErrorStats
                {
                    cameraName = camera.name,
                    objectName = meshPosition.name,
                    errorHistory = new List<float>(),
                    latencyHistory = new List<float>(),
                    lastUpdateTime = DateTime.Now
                };
            }

            float currentError = CalculateCameraError(meshPosition, camera);
            float currentLatency = (float)(DateTime.Now - errorStats[key].lastUpdateTime).TotalMilliseconds;
            var stats = errorStats[key];

            stats.totalError += currentError;
            stats.totalLatency += currentLatency;
            stats.frameCount++;
            stats.averageError = stats.totalError / stats.frameCount;
            stats.averageLatency = stats.totalLatency / stats.frameCount;
            stats.errorHistory.Add(currentError);
            stats.latencyHistory.Add(currentLatency);
            stats.currentLatency = currentLatency;
            stats.lastUpdateTime = DateTime.Now;

            if (isLogging)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    logWriter.WriteLine($"{timestamp},{camera.name},{meshPosition.name},{currentError:F4},{stats.averageError:F4},{stats.totalError:F4},{stats.frameCount},{currentLatency:F2},{stats.averageLatency:F2},{stats.totalLatency:F2}");
                    logWriter.Flush();
                }
                catch (Exception e)
                {
                    Debug.LogError($"寫入誤差記錄時發生錯誤: {e.Message}");
                }
            }
        }
    }

    private float CalculateCameraError(GameObject meshPosition, Camera camera)
    {
        Vector3 screenPoint = camera.WorldToScreenPoint(meshPosition.transform.position);
        Vector3 idealPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, screenPoint.z);
        float pixelError = Vector2.Distance(
            new Vector2(screenPoint.x, screenPoint.y),
            new Vector2(idealPosition.x, idealPosition.y)
        );
        float screenDiagonal = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
        float normalizedError = pixelError / screenDiagonal;

        Vector3 worldPosition = meshPosition.transform.position;
        Vector3 cameraPosition = camera.transform.position;
        float worldDistance = Vector3.Distance(worldPosition, cameraPosition);

        Debug.Log($"Camera: {camera.name}, Object: {meshPosition.name}");
        Debug.Log($"Screen Error (normalized): {normalizedError}");
        Debug.Log($"World Distance (meters): {worldDistance}");

        return worldDistance;
    }

    private string Datatobetransmitted(GameObject meshPosition, Camera camera)
    {
        Vector3 Object_OriginalPosition = meshPosition.transform.position;
        Vector3 Object_ScreenPosition = GetObjectScreenPoint(meshPosition, camera);
        originalsize.x = camera.rect.width * 960;
        originalsize.y = camera.rect.height * 960;

        Caculate_Reprojection_2D reprojectionData = new Caculate_Reprojection_2D
        {
            Object_Screen_norm_x = Object_ScreenPosition.x / originalsize.x,
            Object_Screen_norm_y = Object_ScreenPosition.y / originalsize.y,
            Object_ScreenPosition_z = Object_ScreenPosition.z,
            Object_OriginalPosition_x = Object_OriginalPosition.x,
            Object_OriginalPosition_y = Object_OriginalPosition.y,
            Object_OriginalPosition_z = Object_OriginalPosition.z,
            CameraName = camera.name,
            ObjectName = meshPosition.name
        };

        return JsonConvert.SerializeObject(reprojectionData);
    }

    private Vector3 GetObjectScreenPoint(GameObject meshPosition, Camera camera)
    {
        return camera.WorldToScreenPoint(meshPosition.transform.position);
    }

    public float GetAverageError()
    {
        if (errorStats.Count == 0)
            return 0f;

        float sum = 0f;
        int count = 0;
        foreach (var stats in errorStats.Values)
        {
            if (stats.frameCount > 0)
            {
                sum += stats.averageError;
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }

    public float GetAverageLatency()
    {
        if (errorStats.Count == 0)
            return 0f;

        float sum = 0f;
        int count = 0;
        foreach (var stats in errorStats.Values)
        {
            if (stats.frameCount > 0)
            {
                sum += stats.averageLatency;
                count++;
            }
        }
        return count > 0 ? sum / count : 0f;
    }

    public float GetMaxLatency()
    {
        if (errorStats.Count == 0)
            return 0f;

        float maxLatency = 0f;
        foreach (var stats in errorStats.Values)
        {
            if (stats.latencyHistory.Count > 0)
            {
                float currentMax = stats.latencyHistory.Max();
                maxLatency = Mathf.Max(maxLatency, currentMax);
            }
        }
        return maxLatency;
    }
}
