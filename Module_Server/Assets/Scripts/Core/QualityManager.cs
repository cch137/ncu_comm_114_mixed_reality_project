using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.IO;
using System.Text;
using System;

[ExecuteInEditMode]
public class QualityManager : MonoBehaviour
{
    [System.Serializable]
    public struct QualityConfig
    {
        public float Qk;
        public int width;
        public int height;
        public string compression;
        public int fps;
        public float bandwidthMbps;
    }

    public GameObject usersRoot;
    public GraphInputGenerator graphInputGenerator;
    public AllUpdateObject allUpdateObject;
    public List<QualityConfig> qualityTable = new List<QualityConfig>
    {
        new QualityConfig { Qk = 0.1f, width = 640, height = 480, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.3f, width = 640, height = 480, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.4f, width = 1280, height = 720, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.5f, width = 1280, height = 720, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.7f, width = 1280, height = 720, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.8f, width = 1920, height = 1080, compression = "Medium", fps = 50, bandwidthMbps = 0f },
        new QualityConfig { Qk = 1.0f, width = 1920, height = 1080, compression = "Medium", fps = 50, bandwidthMbps = 0f }
    };

    private List<GameObject> clusters = new List<GameObject>();
    private List<Camera> objectCameras = new List<Camera>();
    private float lastMeasurementTime;
    private float[] Q_k;
    private float[,] V_ku;
    private float[,] B_ku_calc;
    private float[] B_k_true;
    private List<VolumetricUpdate> allObjects;
    private List<Transform> users;
    private float[,] D_ok;

    private float alpha = 1.0f;
    private float beta = 1.0f;
    private float gamma = 0.1f;
    private float epsilon = 0.01f;
    private float B_u_max = 50f;

    private float distanceThreshold = 5f;
    private float speedDiffThreshold = 2f;

    private int lastChildCount = 0;
    private bool isUpdatingClusters = false;
    private float lastUpdateTime = 0f;
    private float updateInterval = 1f;

    // 圖狀態貪婪演算法的常數
    private const float DISTANCE_THRESHOLD = 5f;  // τ = 5m
    private const float SPEED_THRESHOLD = 2f;     // σ = 2m/s
    private const float VISIBILITY_THRESHOLD = 2f;  // θ = 2
    private const float HYSTERESIS_DELTA = 0.5f;   // δ = 0.5
    private const float W_MAX = 10f;               // 權重最大值

    // 圖狀態貪婪演算法的數據結構
    private Dictionary<string, float> objectWeights;  // w(o_i, o_j)
    private Dictionary<string, float> userObjectWeights;  // w(u, o)
    private Dictionary<int, Dictionary<int, bool>> visibilityMatrix;  // V_{k,u}
    private Dictionary<int, float> qualityMatrix;  // Q_k

    void Awake()
    {
        if (usersRoot == null)
        {
            Debug.LogError("UsersRoot is not assigned in GreedyQualityManager!");
            LogToFile("Awake: UsersRoot is not assigned in GreedyQualityManager!");
            return;
        }
        if (graphInputGenerator == null)
        {
            Debug.LogError("GraphInputGenerator is not assigned in GreedyQualityManager!");
            LogToFile("Awake: GraphInputGenerator is not assigned in GreedyQualityManager!");
            return;
        }
        if (allUpdateObject == null)
        {
            Debug.LogError("AllUpdateObject is not assigned in GreedyQualityManager!");
            LogToFile("Awake: AllUpdateObject is not assigned in GreedyQualityManager!");
            return;
        }

        lastChildCount = usersRoot.transform.childCount;
        StartCoroutine(WaitForUsersAndUpdate());

        // 初始化圖狀態貪婪演算法的數據結構
        objectWeights = new Dictionary<string, float>();
        userObjectWeights = new Dictionary<string, float>();
        visibilityMatrix = new Dictionary<int, Dictionary<int, bool>>();
        qualityMatrix = new Dictionary<int, float>();
    }

    private IEnumerator WaitForUsersAndUpdate()
    {
        while (usersRoot.transform.childCount == 0)
        {
            Debug.Log("No users found in UsersRoot. Waiting for users to connect...");
            LogToFile("WaitForUsersAndUpdate: No users found in UsersRoot. Waiting for users to connect...");
            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"Initial users found: {usersRoot.transform.childCount}. Starting UpdateClustersAndQuality...");
        LogToFile($"WaitForUsersAndUpdate: Initial users found: {usersRoot.transform.childCount}. Starting UpdateClustersAndQuality...");
        yield return StartCoroutine(UpdateClustersAndQuality());
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        int currentChildCount = usersRoot.transform.childCount;
        if (currentChildCount != lastChildCount)
        {
            Debug.Log($"UsersRoot child count changed from {lastChildCount} to {currentChildCount}. Triggering UpdateClustersAndQuality...");
            LogToFile($"Update: UsersRoot child count changed from {lastChildCount} to {currentChildCount}. Triggering UpdateClustersAndQuality...");
            lastChildCount = currentChildCount;

            if (Time.time - lastUpdateTime >= updateInterval && !isUpdatingClusters)
            {
                StartCoroutine(UpdateClustersAndQuality());
            }
        }

        if (currentChildCount > 0 && Time.time - lastUpdateTime >= 5f && !isUpdatingClusters)
        {
            Debug.Log("Periodic update triggered after 5 seconds.");
            LogToFile("Update: Periodic update triggered after 5 seconds.");
            StartCoroutine(UpdateClustersAndQuality());
        }
    }

    private IEnumerator UpdateClustersAndQuality()
    {
        if (isUpdatingClusters)
        {
            Debug.Log("UpdateClustersAndQuality is already running. Skipping...");
            LogToFile("UpdateClustersAndQuality: Already running. Skipping...");
            yield break;
        }

        isUpdatingClusters = true;
        lastUpdateTime = Time.time;

        try
        {
            // 檢查必要組件
            if (usersRoot == null)
            {
                Debug.LogError("UsersRoot is not assigned!");
                isUpdatingClusters = false;
                yield break;
            }

            if (allUpdateObject == null)
            {
                Debug.LogError("AllUpdateObject is not assigned!");
                isUpdatingClusters = false;
                yield break;
            }

            // 檢查相機
            objectCameras = FindObjectCameras(usersRoot);
            if (objectCameras.Count == 0)
            {
                Debug.LogError("No object cameras found!");
                isUpdatingClusters = false;
                yield break;
            }

            float startTime = Time.time;
            Debug.Log($"UpdateClustersAndQuality: Starting at {startTime}...");
            LogToFile($"UpdateClustersAndQuality: Starting at {startTime}...");

            // 修改清除舊clusters的邏輯
            Debug.Log("Updating clusters...");
            LogToFile("UpdateClustersAndQuality: Updating clusters...");
            foreach (var clusterObj in clusters)
            {
                if (clusterObj != null)
                {
                    // 保存物件的父節點
                    Transform[] children = new Transform[clusterObj.transform.childCount];
                    for (int i = 0; i < clusterObj.transform.childCount; i++)
                    {
                        children[i] = clusterObj.transform.GetChild(i);
                    }

                    // 暫時將物件移到根節點
                    foreach (var child in children)
                    {
                        child.SetParent(null);
                    }

                    Destroy(clusterObj);
                }
            }
            clusters.Clear();

            // 取得 VolumetricUpdate 物件
            Debug.Log("Fetching VolumetricUpdate objects...");
            LogToFile("UpdateClustersAndQuality: Fetching VolumetricUpdate objects...");
            allObjects = allUpdateObject.Object_list_get();
            if (allObjects == null || allObjects.Count == 0)
            {
                Debug.LogError("No objects found in AllUpdateObject!");
                LogToFile("UpdateClustersAndQuality: No objects found in AllUpdateObject! Please ensure AllUpdateObject has initialized VolumetricUpdate objects.");
                isUpdatingClusters = false;
                yield break;
            }
            Debug.Log($"Found {allObjects.Count} VolumetricUpdate objects in AllUpdateObject. Time: {Time.time - startTime}s");
            LogToFile($"Found {allObjects.Count} VolumetricUpdate objects in AllUpdateObject. Time: {Time.time - startTime}s");

            // 取得users
            Debug.Log("Fetching users...");
            LogToFile("UpdateClustersAndQuality: Fetching users...");
            users = new List<Transform>();
            foreach (Transform user in usersRoot.transform)
            {
                users.Add(user);
                Debug.Log($"Added user: {user.name}");
                LogToFile($"UpdateClustersAndQuality: Added user: {user.name}");
            }
            if (users.Count == 0)
            {
                Debug.LogError("No users found in UsersRoot!");
                LogToFile("UpdateClustersAndQuality: No users found in UsersRoot!");
                isUpdatingClusters = false;
                yield break;
            }

            // 生成graph input
            Debug.Log("Generating graph input...");
            LogToFile("UpdateClustersAndQuality: Generating graph input...");
            GraphInputGenerator.GraphInput graphInput = graphInputGenerator.GenerateInput();
            float[,] w_oo = graphInput.w_oo;
            float[][] w_uo = graphInput.w_uo;
            Debug.Log($"Graph input generated. Time: {Time.time - startTime}s");
            LogToFile($"UpdateClustersAndQuality: Graph input generated. Time: {Time.time - startTime}s");

            // 進行greedy clustering
            Debug.Log("Performing greedy clustering...");
            LogToFile("UpdateClustersAndQuality: Performing greedy clustering...");
            List<List<VolumetricUpdate>> clusterList = GreedyClustering(allObjects, w_oo);
            int K = clusterList.Count;
            Debug.Log($"Created {K} clusters. Time: {Time.time - startTime}s");
            LogToFile($"UpdateClustersAndQuality: Created {K} clusters. Time: {Time.time - startTime}s");

            // 計算D_ok
            Debug.Log("Calculating D_ok...");
            LogToFile("UpdateClustersAndQuality: Calculating D_ok...");
            D_ok = new float[allObjects.Count, K];
            for (int o = 0; o < allObjects.Count; o++)
            {
                for (int k = 0; k < K; k++)
                {
                    D_ok[o, k] = clusterList[k].Contains(allObjects[o]) ? 1f : 0f;
                }
                if (o % 5 == 0)
                {
                    yield return null;
                }
            }

            // 更新權重
            UpdateWeights();

            // 計算可見性矩陣
            CalculateVisibilityMatrix(clusterList);

            // 計算品質矩陣
            CalculateQualityMatrix(clusterList);

            // 計算Q_k, V_ku, B_ku_calc, B_k_true
            Debug.Log("Calculating Q_k, V_ku, B_ku_calc, B_k_true...");
            LogToFile("UpdateClustersAndQuality: Calculating Q_k, V_ku, B_ku_calc, B_k_true...");
            Q_k = new float[K];
            V_ku = new float[K, users.Count];
            B_ku_calc = new float[K, users.Count];
            B_k_true = new float[K];

            for (int k = 0; k < K; k++)
            {
                GameObject clusterObj = new GameObject($"Cluster_{k}");
                foreach (var obj in clusterList[k])
                {
                    obj.OBJ_Pos.transform.SetParent(clusterObj.transform);
                }
                clusters.Add(clusterObj);

                // 使用新的可見性計算
                for (int u = 0; u < users.Count; u++)
                {
                    V_ku[k, u] = visibilityMatrix[k][u] ? 1f : 0f;
                }

                // 使用新的品質計算
                Q_k[k] = qualityMatrix[k];

                // 計算B_ku_calc
                for (int u = 0; u < users.Count; u++)
                {
                    B_ku_calc[k, u] = V_ku[k, u] * 2.8f * (Mathf.Exp(2.5f * Q_k[k]) - 1);
                }

                // 匹配QualityConfig
                QualityConfig config = qualityTable.Find(c => Mathf.Abs(c.Qk - Q_k[k]) < 0.01f);
                B_k_true[k] = config.bandwidthMbps;

                yield return null;
            }

            // 在計算完Q_k, V_ku, B_ku_calc, B_k_true後立即輸出
            LogOutputResults(K);

            // 保存結果到CSV
            SaveResultsToCSV(K);

            // 確保物件持續存在
            foreach (var clusterObj in clusters)
            {
                if (clusterObj != null)
                {
                    DontDestroyOnLoad(clusterObj);
                }
            }

            // 定期更新
            if (!IsInvoking(nameof(PeriodicUpdate)))
            {
                InvokeRepeating(nameof(PeriodicUpdate), 1f, 1f);
            }
        }
        finally
        {
            isUpdatingClusters = false;
        }
    }

    private void PeriodicUpdate()
    {
        if (!isUpdatingClusters && usersRoot != null && usersRoot.transform.childCount > 0)
        {
            StartCoroutine(UpdateClustersAndQuality());
        }
    }

    private void SaveResultsToCSV(int K)
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Greedy_Logs");
        string csvFileName = $"greedy_output_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string csvPath = Path.Combine(logDir, csvFileName);

        try
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
                Debug.Log($"Created directory: {logDir}");
            }

            using (StreamWriter writer = new StreamWriter(csvPath, false, Encoding.UTF8))
            {
                // 寫入標題
                writer.WriteLine("Timestamp,Cluster,Q_k,V_ku,B_ku_calc,B_k_true");

                // 寫入數據
                string timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                for (int k = 0; k < K; k++)
                {
                    string v_ku = string.Join(",", Enumerable.Range(0, users.Count).Select(u => V_ku[k, u]));
                    string b_ku_calc = string.Join(",", Enumerable.Range(0, users.Count).Select(u => B_ku_calc[k, u]));

                    writer.WriteLine($"{timestamp},{k},{Q_k[k]},{v_ku},{b_ku_calc},{B_k_true[k]}");
                }
            }

            Debug.Log($"Results saved to CSV: {csvPath}");
            LogToFile($"Results saved to CSV: {csvPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save results to CSV: {e.Message}");
            LogToFile($"Failed to save results to CSV: {e.Message}");
        }
    }

    private void LogOutputResults(int K)
    {
        LogToFile("Output Results: Q_k");
        for (int k = 0; k < Q_k.Length; k++)
        {
            LogToFile($"Cluster{k}: {Q_k[k]}");
        }

        LogToFile("Output Results: V_ku");
        for (int k = 0; k < V_ku.GetLength(0); k++)
        {
            List<float> row = new List<float>();
            for (int u = 0; u < V_ku.GetLength(1); u++)
            {
                row.Add(V_ku[k, u]);
            }
            LogToFile($"Cluster{k}: [{string.Join(", ", row)}]");
        }

        LogToFile("Output Results: B_ku_calc");
        for (int k = 0; k < B_ku_calc.GetLength(0); k++)
        {
            List<float> row = new List<float>();
            for (int u = 0; u < B_ku_calc.GetLength(1); u++)
            {
                row.Add(B_ku_calc[k, u]);
            }
            LogToFile($"Cluster{k}: [{string.Join(", ", row)}]");
        }

        LogToFile("Output Results: B_k_true");
        for (int k = 0; k < B_k_true.Length; k++)
        {
            LogToFile($"Cluster{k}: {B_k_true[k]}");
        }

        LogToFile("Output Results: D_ok");
        for (int o = 0; o < D_ok.GetLength(0); o++)
        {
            List<float> row = new List<float>();
            for (int k = 0; k < D_ok.GetLength(1); k++)
            {
                row.Add(D_ok[o, k]);
            }
            LogToFile($"Object{o}: [{string.Join(", ", row)}]");
        }
    }

    private void LogToFile(string message)
    {
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Greedy_Logs");
        string logPath = Path.Combine(logDir, "GreedyQualityManager_Log.txt");
        try
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
                Debug.Log($"Created directory: {logDir}");
            }

            using (StreamWriter writer = new StreamWriter(logPath, true))
            {
                writer.WriteLine($"Timestamp: {DateTime.Now}");
                writer.WriteLine(message);
                writer.WriteLine("--------------------");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to log to file: {e.Message}");
        }
    }

    private List<List<VolumetricUpdate>> GreedyClustering(List<VolumetricUpdate> objects, float[,] w_oo)
    {
        float startTime = Time.time;
        Debug.Log("GreedyClustering: Starting...");
        LogToFile($"GreedyClustering: Starting at {startTime}...");

        List<List<VolumetricUpdate>> clusters = new List<List<VolumetricUpdate>>();
        List<VolumetricUpdate> unassigned = new List<VolumetricUpdate>(objects);
        int iteration = 0;

        while (unassigned.Count > 0)
        {
            if (usersRoot.transform.childCount == 0)
            {
                Debug.Log($"GreedyClustering: UsersRoot became empty during clustering (iteration {iteration}). Aborting...");
                LogToFile($"GreedyClustering: UsersRoot became empty during clustering (iteration {iteration}). Aborting... Time: {Time.time - startTime}s");
                return clusters;
            }

            VolumetricUpdate seed = unassigned[0];
            List<VolumetricUpdate> cluster = new List<VolumetricUpdate> { seed };
            unassigned.Remove(seed);

            List<VolumetricUpdate> toRemove = new List<VolumetricUpdate>();
            foreach (var obj in unassigned)
            {
                int i = objects.IndexOf(seed);
                int j = objects.IndexOf(obj);
                float dist = Vector3.Distance(seed.OBJ_Pos.transform.position, obj.OBJ_Pos.transform.position);
                float speedDiff = Mathf.Abs(seed.speed - obj.speed);

                if (dist < distanceThreshold && speedDiff < speedDiffThreshold && w_oo[i, j] < float.MaxValue)
                {
                    cluster.Add(obj);
                    toRemove.Add(obj);
                }
            }

            foreach (var obj in toRemove)
            {
                unassigned.Remove(obj);
            }

            clusters.Add(cluster);
            iteration++;
        }

        Debug.Log($"GreedyClustering: Completed. Total clusters: {clusters.Count}. Time: {Time.time - startTime}s");
        LogToFile($"GreedyClustering: Completed. Total clusters: {clusters.Count}. Time: {Time.time - startTime}s");
        return clusters;
    }

    private List<Camera> FindObjectCameras(GameObject usersRoot)
    {
        List<Camera> cameras = new List<Camera>();

        // 使用更寬鬆的查找方式
        Camera[] allCameras = usersRoot.GetComponentsInChildren<Camera>(true);
        foreach (Camera cam in allCameras)
        {
            // 檢查相機是否屬於物件相機
            if (cam.name.Contains("Object") || cam.name.Contains("Render"))
            {
                cameras.Add(cam);
                Debug.Log($"Found camera: {cam.name}");
            }
        }

        return cameras;
    }

    private void MeasureBandwidth()
    {
        float currentTime = Time.time;
        float deltaTime = currentTime - lastMeasurementTime;

        if (deltaTime <= 0) return;

        // 計算W e帶寬
        float uploadBandwidthMbps = 5.0f;
        float downloadBandwidthMbps = 3.0f;

        for (int i = 0; i < clusters.Count; i++)
        {
            Debug.Log($"Cluster {i} Bandwidth - Upload: {uploadBandwidthMbps:F2} Mbps, Download: {downloadBandwidthMbps:F2} Mbps");
            LogToFile($"Cluster {i} Bandwidth - Upload: {uploadBandwidthMbps:F2} Mbps, Download: {downloadBandwidthMbps:F2} Mbps");

            if (i < Q_k.Length)
            {
                QualityConfig config = qualityTable.Find(c => Mathf.Abs(c.Qk - Q_k[i]) < 0.01f);
                if (config.Qk != 0)
                {
                    int index = qualityTable.IndexOf(config);
                    config.bandwidthMbps = uploadBandwidthMbps;
                    qualityTable[index] = config;

                    if (i < B_k_true.Length)
                    {
                        B_k_true[i] = uploadBandwidthMbps;
                    }
                }
            }
        }

        lastMeasurementTime = currentTime;

        float loss = 0f;
        float totalBandwidthCalc = 0f;
        for (int k = 0; k < clusters.Count; k++)
        {
            for (int u = 0; u < users.Count; u++)
            {
                totalBandwidthCalc += B_ku_calc[k, u];
                if (V_ku[k, u] > 0f)
                {
                    float bandwidthError = (B_ku_calc[k, u] - B_k_true[k]) / Mathf.Max(B_k_true[k], 0.1f);
                    float qualityTerm = 1f / (Q_k[k] + epsilon);
                    loss += V_ku[k, u] * (
                        alpha * bandwidthError * bandwidthError +
                        beta * qualityTerm
                    );
                }
            }
        }

        float bandwidthPenalty = Mathf.Max(0f, totalBandwidthCalc - B_u_max);
        loss += gamma * bandwidthPenalty * bandwidthPenalty;

        Debug.Log($"GNN Loss (Updated): {loss}");
        LogToFile($"GNN Loss (Updated): {loss}");
    }

    public void ApplyQualitySettings(float Qk, GameObject cluster, Camera clusterCamera)
    {
        if (Qk <= 0f) return;

        QualityConfig config = qualityTable.Find(c => Mathf.Abs(c.Qk - Qk) < 0.01f);
        if (config.Qk == 0) return;

        if (clusterCamera != null)
        {
            RenderTexture rt = new RenderTexture(config.width, config.height, 24);
            rt.Create();
            clusterCamera.targetTexture = rt;
        }

        Application.targetFrameRate = config.fps;

        float qualityLevel = config.compression == "High" ? 0.1f : (config.compression == "Medium" ? 0.5f : 1.0f);
        foreach (Renderer renderer in cluster.GetComponentsInChildren<Renderer>())
        {
            if (renderer.material.HasProperty("_QualityLevel"))
                renderer.material.SetFloat("_QualityLevel", qualityLevel);
        }

        Debug.Log($"Cluster Quality: Qk={Qk}, Resolution={config.width}x{config.height}, FPS={config.fps}, Compression={config.compression}, Bandwidth={config.bandwidthMbps:F2} Mbps");
        LogToFile($"Cluster Quality: Qk={Qk}, Resolution={config.width}x{config.height}, FPS={config.fps}, Compression={config.compression}, Bandwidth={config.bandwidthMbps:F2} Mbps");
    }

    void OnDestroy()
    {
        foreach (var clusterObj in clusters)
        {
            if (clusterObj != null)
            {
                Destroy(clusterObj);
            }
        }
        clusters.Clear();
    }

    public float[,] GetVku() => V_ku;
    public float[] GetQk() => Q_k;
    public float[,] GetBkuCalc() => B_ku_calc;
    public float[] GetBkTrue() => B_k_true;
    public float[,] GetDok() => D_ok;

    private void UpdateWeights()
    {
        objectWeights.Clear();
        userObjectWeights.Clear();

        // 更新物件之間的權重
        for (int i = 0; i < allObjects.Count; i++)
        {
            for (int j = i + 1; j < allObjects.Count; j++)
            {
                float distance = Vector3.Distance(allObjects[i].OBJ_Pos.transform.position, allObjects[j].OBJ_Pos.transform.position);
                float speedDiff = Mathf.Abs(allObjects[i].speed - allObjects[j].speed);
                float weight = distance * 0.1f + speedDiff * 0.1f;

                string key1 = $"{i}_{j}";
                string key2 = $"{j}_{i}";
                objectWeights[key1] = weight;
                objectWeights[key2] = weight;
            }
        }

        // 更新用戶-物件權重
        foreach (var user in users)
        {
            for (int i = 0; i < allObjects.Count; i++)
            {
                float distance = Vector3.Distance(user.position, allObjects[i].OBJ_Pos.transform.position);
                float weight = distance * 0.1f;

                string key = $"{user.GetInstanceID()}_{i}";
                userObjectWeights[key] = weight;
            }
        }
    }

    private void CalculateVisibilityMatrix(List<List<VolumetricUpdate>> clusters)
    {
        visibilityMatrix.Clear();

        foreach (var cluster in clusters)
        {
            int clusterId = clusters.IndexOf(cluster);
            visibilityMatrix[clusterId] = new Dictionary<int, bool>();

            foreach (var user in users)
            {
                float totalWeight = 0f;
                foreach (var obj in cluster)
                {
                    int objIndex = allObjects.IndexOf(obj);
                    string key = $"{user.GetInstanceID()}_{objIndex}";
                    if (userObjectWeights.ContainsKey(key))
                    {
                        totalWeight += userObjectWeights[key];
                    }
                }

                bool isVisible = totalWeight < VISIBILITY_THRESHOLD;

                // 應用遲滯機制
                if (visibilityMatrix[clusterId].ContainsKey(user.GetInstanceID()) &&
                    visibilityMatrix[clusterId][user.GetInstanceID()] && !isVisible)
                {
                    isVisible = totalWeight <= (VISIBILITY_THRESHOLD + HYSTERESIS_DELTA);
                }

                visibilityMatrix[clusterId][user.GetInstanceID()] = isVisible;
            }
        }
    }

    private void CalculateQualityMatrix(List<List<VolumetricUpdate>> clusters)
    {
        qualityMatrix.Clear();

        foreach (var cluster in clusters)
        {
            int clusterId = clusters.IndexOf(cluster);
            float maxQuality = 0.5f;  // 最小品質

            foreach (var user in users)
            {
                if (visibilityMatrix[clusterId][user.GetInstanceID()])
                {
                    float totalWeight = 0f;
                    foreach (var obj in cluster)
                    {
                        int objIndex = allObjects.IndexOf(obj);
                        string key = $"{user.GetInstanceID()}_{objIndex}";
                        if (userObjectWeights.ContainsKey(key))
                        {
                            totalWeight += userObjectWeights[key];
                        }
                    }

                    // 計算基礎品質
                    float baseQuality = 1f - (totalWeight / (cluster.Count * W_MAX));

                    // 計算距離變異數
                    float distanceVariance = CalculateDistanceVariance(user, cluster);

                    // 根據變異數調整品質
                    float adjustedQuality = distanceVariance < 1f ?
                        baseQuality : 0.5f * baseQuality;

                    // 確保品質在 [0.5, 1] 範圍內
                    adjustedQuality = Mathf.Max(0.5f, Mathf.Min(1f, adjustedQuality));

                    // 更新最大品質
                    maxQuality = Mathf.Max(maxQuality, adjustedQuality);
                }
            }

            qualityMatrix[clusterId] = maxQuality;
        }
    }

    private float CalculateDistanceVariance(Transform user, List<VolumetricUpdate> cluster)
    {
        List<float> distances = new List<float>();
        foreach (var obj in cluster)
        {
            distances.Add(Vector3.Distance(user.position, obj.OBJ_Pos.transform.position));
        }
        return CalculateVariance(distances);
    }

    private float CalculateVariance(List<float> values)
    {
        float mean = values.Average();
        float sumSquaredDiff = values.Sum(x => Mathf.Pow(x - mean, 2));
        return sumSquaredDiff / values.Count;
    }
}