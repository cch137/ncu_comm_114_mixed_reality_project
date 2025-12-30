using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Diagnostics;  // 新增：用於 Stopwatch

public class GraphManager : MonoBehaviour
{
    [SerializeField]
    private float clusteringThreshold = 5.0f;
    [SerializeField]
    private bool enableLogging = true;
    [SerializeField]
    private string logFilePath = "Logs/project/0609_clustering_log.txt";

    // 添加 SerializeField 列表來直接抓取節點
    [SerializeField]
    private List<GameObject> nodeObjects = new List<GameObject>();

    private static GraphManager instance;
    public static GraphManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GraphManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GraphManager");
                    instance = go.AddComponent<GraphManager>();
                }
            }
            return instance;
        }
    }

    private GraphBuilder graphBuilder;
    private List<VolumetricUpdate> volumetricObjects;
    private List<GameObject> userObjects;
    private List<ClusterInfo> clusterInfos;

    // 添加 PositionSet 引用
    [SerializeField]
    private PositionSet positionSet;

    // 添加 AllUpdateObject 引用
    [SerializeField]
    private AllUpdateObject allUpdateObject;

    private float lastLogTime = 0f;
    private const float LOG_INTERVAL = 0f;  // 10秒記錄一次

    private int frameCount = 0;
    private const int UPDATE_FRAME_INTERVAL = 10;  // 每 10 幀更新一次

    // 常數定義
    private const float USER_BANDWIDTH = 50f;  // 50Mbps
    private const float ALPHA = 1.0f;         // a 參數
    private const float N = 1.0f;             // n 參數
    private const float DELTA_TIME = 0.1f;    // 時間間隔

    // 添加參數定義
    private const float VISIBILITY_DISTANCE = 10f;  // 原有的可見性距離閾值
    private float epsilon = 1.0f;                   // 距離變化閾值
    private float gamma = 0.0001f;                     // 誤差閾值

    // 新增：效能測量相關變數
    private Stopwatch frameStopwatch = new Stopwatch();
    private List<float> iterationCounts = new List<float>();
    private List<float> frameTimes = new List<float>();
    private const int MAX_SAMPLES = 100;  // 最多保存100個樣本
    private string performanceLogPath;

    // 叢集品質優化最大迭代次數設為 5
    private const int MAX_ITERATIONS = 5;

    private float[] userBandwidths; // 新增：用於跨 frame 共用 userBandwidths

    public class ClusterInfo
    {
        public int clusterId;
        public List<VolumetricUpdate> objects;
        public VolumetricUpdate mainCamera;
        public List<int> targetLayers;
        public List<GameObject> visibleUsers = new List<GameObject>();
        public float qualityLevel = 1.0f;

        public ClusterInfo(int id)
        {
            clusterId = id;
            objects = new List<VolumetricUpdate>();
            targetLayers = new List<int>();
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // 在 Release Mode 下使用 Application.persistentDataPath
#if UNITY_EDITOR
    // Editor: write logs into project-root Logs/project (parent of Assets)
    performanceLogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "project", "performance_metrics.txt"));
#else
        // Runtime: write to persistent data path for safety
        performanceLogPath = System.IO.Path.Combine(Application.persistentDataPath, "performance_metrics.txt");
#endif

        // Normalize serialized/inspector-provided logFilePath to point under project-root Logs/project
        if (!Path.IsPathRooted(logFilePath))
        {
            string filename = Path.GetFileName(logFilePath);
            // Normalize to project-root Logs/project
            logFilePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "project", filename));
        }
    }

    void Start()
    {
        graphBuilder = new GraphBuilder();
        volumetricObjects = new List<VolumetricUpdate>();
        userObjects = new List<GameObject>();
        clusterInfos = new List<ClusterInfo>();

        // 初始化所有 Object 節點，只做一次
        foreach (var nodeObj in nodeObjects)
        {
            if (nodeObj == null) continue;
            Vector3 position = nodeObj.transform.position;
            graphBuilder.AddObjectNode(nodeObj.name, position, 0f, 0f);
        }

        // 如果沒有設置 PositionSet，嘗試在場景中尋找
        if (positionSet == null)
        {
            positionSet = FindObjectOfType<PositionSet>();
            if (positionSet == null)
            {
                UnityEngine.Debug.LogWarning("找不到 PositionSet 組件，請確保場景中有此組件。");
            }
        }

        // 如果沒有設置 AllUpdateObject，嘗試在場景中尋找
        if (allUpdateObject == null)
        {
            allUpdateObject = FindObjectOfType<AllUpdateObject>();
            if (allUpdateObject == null)
            {
                UnityEngine.Debug.LogWarning("找不到 AllUpdateObject 組件，請確保場景中有此組件。");
            }
        }
    }

    public void AddVolumetricObject(VolumetricUpdate obj)
    {
        if (!volumetricObjects.Contains(obj))
        {
            volumetricObjects.Add(obj);
        }
    }

    public void AddUserObject(GameObject user)
    {
        if (!userObjects.Contains(user))
        {
            userObjects.Add(user);
        }
    }

    private List<Transform> FindUserCameras()
    {
        List<Transform> userCameras = new List<Transform>();
        GameObject usersParent = GameObject.Find("Users");
        if (usersParent == null)
        {
            UnityEngine.Debug.LogWarning("找不到 Users 物件！");
            return userCameras;
        }
        foreach (Transform user in usersParent.transform)
        {
            Camera[] cameras = user.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                // 你可以根據命名規則過濾
                if (cam.gameObject.name.Contains("User_Position"))
                {
                    userCameras.Add(cam.transform);
                }
            }
        }
        return userCameras;
    }

    public void UpdateGraph()
    {
        // 步驟 1：建立交互圖（保留原有的 BuildGraph 邏輯）
        graphBuilder.BuildGraph();

        // 步驟 3：迭代精煉 - 更新邊權重
        var edges = graphBuilder.GetEdges();
        var nodes = graphBuilder.GetNodes();
        var userCameras = FindUserCameras();

        foreach (var edge in edges)
        {
            if (edge.source.isUser || edge.target.isUser) continue;

            // 找到這兩個物件所屬的群集
            var sourceVolumetric = volumetricObjects.FirstOrDefault(v => v.name == edge.source.id);
            var targetVolumetric = volumetricObjects.FirstOrDefault(v => v.name == edge.target.id);

            if (sourceVolumetric != null && targetVolumetric != null)
            {
                var sourceCluster = clusterInfos.FirstOrDefault(c => c.objects.Contains(sourceVolumetric));
                var targetCluster = clusterInfos.FirstOrDefault(c => c.objects.Contains(targetVolumetric));

                if (sourceCluster != null && targetCluster != null)
                {
                    // 計算共享使用者數量（ΔB(oi, oj)）
                    int sharedUsers = 0;
                    foreach (var cam in userCameras)
                    {
                        bool sourceVisible = sourceCluster.visibleUsers.Contains(cam.gameObject);
                        bool targetVisible = targetCluster.visibleUsers.Contains(cam.gameObject);
                        if (sourceVisible && targetVisible)
                        {
                            sharedUsers++;
                        }
                    }

                    // 計算頻寬節省
                    float bandwidthSaving = sharedUsers * USER_BANDWIDTH;

                    // 更新邊權重 w*(oi, oj) = w(oi, oj) - γ * ΔB(oi, oj)
                    float newWeight = edge.weight - gamma * bandwidthSaving;
                    edge.weight = Mathf.Max(0.1f, newWeight);  // 確保權重不會小於 0.1

                    UnityEngine.Debug.Log($"步驟 3 - 更新邊權重：\n" +
                             $"邊: {edge.source.id} - {edge.target.id}\n" +
                             $"原始權重: {edge.weight:F2}\n" +
                             $"共享使用者數: {sharedUsers}\n" +
                             $"頻寬節省 (ΔB): {bandwidthSaving:F2}\n" +
                             $"gamma: {gamma}\n" +
                             $"新權重 (w*): {edge.weight:F2}");
                }
            }
        }

        // 更新分群
        UpdateClusters();
    }

    private void UpdateClusters()
    {
        // === [EVT/Weibull 門檻值自動計算] ===
        // 1. 收集所有物件間的距離變化樣本
        List<double> distanceDeltas = new List<double>();
        var edges = graphBuilder.GetEdges();
        foreach (var edge in edges)
        {
            if (edge.source.isUser || edge.target.isUser) continue;
            // 取得前後位置
            var node1 = edge.source;
            var node2 = edge.target;
            float currentDistance = Vector3.Distance(node1.position, node2.position);
            float previousDistance = Vector3.Distance(node1.previousPosition, node2.previousPosition);
            float delta = Mathf.Abs(currentDistance - previousDistance);
            if (!float.IsNaN(delta) && !float.IsInfinity(delta))
                distanceDeltas.Add(delta);
        }
        // 2. 若樣本數足夠，進行 Weibull 擬合
        if (distanceDeltas.Count >= 3)
        {
            try
            {
                var fitResult = WeibullFitter.Fit(distanceDeltas);
                double mu = fitResult.Location;
                double sigma = fitResult.Scale;
                double xi = fitResult.Shape;
                double p = 0.99; // 信賴度
                // EVT門檻值公式: τ = μ + σ * ( -ln(1-p) )^(1/ξ )
                double tau = mu + sigma * Math.Pow(-Math.Log(1 - p), 1.0 / xi);
                epsilon = (float)tau;
                UnityEngine.Debug.Log($"[EVT] 自動計算距離變化閾值 epsilon = {epsilon:F4} (μ={mu:F4}, σ={sigma:F4}, ξ={xi:F4}, p={p})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EVT] Weibull 擬合失敗: {ex.Message}");
            }
        }
        // === [EVT/Weibull 門檻值自動計算結束] ===

        // 清除現有的群集資訊
        clusterInfos.Clear();

        // 獲取所有節點
        var nodes = graphBuilder.GetNodes();

        // 初始化節點到群集的映射
        Dictionary<string, int> nodeToCluster = new Dictionary<string, int>();
        int currentClusterId = 1;

        // 首先處理所有非使用者節點
        foreach (var node in nodes.Values)
        {
            if (node.isUser) continue;
            if (nodeToCluster.ContainsKey(node.id)) continue;

            // 找到對應的 VolumetricUpdate 物件
            var volumetricObj = volumetricObjects.FirstOrDefault(v => v.name == node.id);
            if (volumetricObj == null) continue;

            // 創建新的群集
            nodeToCluster[node.id] = currentClusterId;
            var clusterInfo = new ClusterInfo(currentClusterId);
            clusterInfo.objects.Add(volumetricObj);

            // 找到所有相連的節點
            var connectedNodes = edges
                .Where(e => (e.source == node || e.target == node) && !e.source.isUser && !e.target.isUser)
                .Select(e => e.source == node ? e.target : e.source)
                .Where(n => !nodeToCluster.ContainsKey(n.id));

            foreach (var connectedNode in connectedNodes)
            {
                if (nodeToCluster.ContainsKey(connectedNode.id)) continue;

                // 找到對應的 VolumetricUpdate 物件
                var connectedVolumetric = volumetricObjects.FirstOrDefault(v => v.name == connectedNode.id);
                if (connectedVolumetric == null) continue;

                // 檢查邊權重是否超過閾值
                var edge = edges.FirstOrDefault(e =>
                    (e.source == node && e.target == connectedNode) ||
                    (e.source == connectedNode && e.target == node));

                if (edge != null && edge.weight >= epsilon)
                {
                    nodeToCluster[connectedNode.id] = currentClusterId;
                    clusterInfo.objects.Add(connectedVolumetric);
                }
            }

            clusterInfos.Add(clusterInfo);
            currentClusterId++;
        }

        // 更新每個群集的可見使用者
        var userCameras = FindUserCameras();
        foreach (var cluster in clusterInfos)
        {
            cluster.visibleUsers.Clear();
            foreach (var cam in userCameras)
            {
                if (cluster.objects.Any(obj => Vector3.Distance(cam.position, obj.position) <= VISIBILITY_DISTANCE))
                {
                    cluster.visibleUsers.Add(cam.gameObject);
                }
            }
        }

        // 保存前一次的群集狀態
        List<List<GameObject>> previousClusters = new List<List<GameObject>>();
        foreach (var cluster in clusterInfos)
        {
            previousClusters.Add(cluster.objects.Select(v => v.OBJ_Pos).ToList());
        }

        // 更新群集後檢查收斂
        List<List<GameObject>> currentClusters = new List<List<GameObject>>();
        foreach (var cluster in clusterInfos)
        {
            currentClusters.Add(cluster.objects.Select(v => v.OBJ_Pos).ToList());
        }

        if (IsConverged(previousClusters, currentClusters))
        {
            UnityEngine.Debug.Log("群集已收斂，應用最終渲染設定");
            ApplyRenderingSettings(clusterInfos);
        }

        // 輸出分群結果
        LogClusteringResults();
    }

    private void LogClusteringResults()
    {
        // 檢查是否達到記錄間隔
        if (Time.time - lastLogTime < LOG_INTERVAL)
        {
            return;
        }
        lastLogTime = Time.time;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logContent = $"\n=== 分群結果 ({timestamp}) ===\n";
        logContent += $"分群閾值: {clusteringThreshold}\n";
        logContent += $"總群組數: {clusterInfos.Count}\n\n";

        // 記錄物件之間的權重
        logContent += "=== 物件之間的權重 ===\n";
        var edges = graphBuilder.GetEdges();
        var objectEdges = edges.Where(e => !e.source.isUser && !e.target.isUser).ToList();
        foreach (var edge in objectEdges)
        {
            logContent += $"物件 {edge.source.id} 到 {edge.target.id} 的權重: {edge.weight:F2}\n";
        }
        logContent += "\n";

        // 記錄使用者與物件之間的權重
        logContent += "=== 使用者與物件之間的權重 ===\n";
        var userEdges = edges.Where(e => e.source.isUser || e.target.isUser).ToList();
        foreach (var edge in userEdges)
        {
            string userNode = edge.source.isUser ? edge.source.id : edge.target.id;
            string objectNode = edge.source.isUser ? edge.target.id : edge.source.id;
            logContent += $"使用者 {userNode} 到物件 {objectNode} 的權重: {edge.weight:F2}\n";
        }
        logContent += "\n";

        // 記錄群組資訊
        foreach (var cluster in clusterInfos)
        {
            logContent += $"群組 {cluster.clusterId}:\n";
            logContent += $"  主相機: {cluster.mainCamera?.name ?? "無"}\n";
            logContent += $"  物件數量: {cluster.objects.Count}\n";
            logContent += $"  物件列表:\n";
            foreach (var obj in cluster.objects)
            {
                logContent += $"    - {obj.name} (Layer: {obj.layermask})\n";
            }
            logContent += $"  目標層級: {string.Join(", ", cluster.targetLayers)}\n\n";
        }

        // 輸出到 Debug.Log
        UnityEngine.Debug.Log(logContent);

        // 寫入檔案
        try
        {
            // 確保目錄存在
            string directory = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 寫入檔案
            File.AppendAllText(logFilePath, logContent);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"寫入分群記錄時發生錯誤: {e.Message}");
        }
    }

    public List<ClusterInfo> GetClusterInfos()
    {
        return clusterInfos;
    }

    public List<GraphBuilder.Edge> GetGraphEdges()
    {
        return graphBuilder.GetEdges();
    }

    public Dictionary<string, GraphBuilder.Node> GetGraphNodes()
    {
        return graphBuilder.GetNodes();
    }

    public float GetUserClusterWeight(GameObject user, VolumetricUpdate clusterMainCamera)
    {
        var edges = graphBuilder.GetEdges();
        var userNode = graphBuilder.GetNodes().Values.FirstOrDefault(n => n.id == user.name);
        var clusterNode = graphBuilder.GetNodes().Values.FirstOrDefault(n => n.id == clusterMainCamera.name);

        if (userNode != null && clusterNode != null)
        {
            // 尋找使用者節點到群集主相機節點的邊
            var edge = edges.FirstOrDefault(e =>
                (e.source == userNode && e.target == clusterNode) ||
                (e.source == clusterNode && e.target == userNode));

            if (edge != null)
            {
                return edge.weight;
            }
        }

        // 如果找不到邊，返回一個預設值
        return 1.0f;
    }

    public float GetClusterWeight(VolumetricUpdate clusterMainCamera)
    {
        var edges = graphBuilder.GetEdges();
        var clusterNode = graphBuilder.GetNodes().Values.FirstOrDefault(n => n.id == clusterMainCamera.name);

        if (clusterNode != null)
        {
            // 計算群集中所有邊的權重總和
            float totalWeight = 0f;
            int edgeCount = 0;

            foreach (var edge in edges.Where(e =>
                (e.source == clusterNode || e.target == clusterNode) &&
                !e.source.isUser && !e.target.isUser))
            {
                totalWeight += edge.weight;
                edgeCount++;
            }

            if (edgeCount > 0)
            {
                return totalWeight / edgeCount;
            }
        }

        return 1.0f;
    }

    // 修改：獲取建議的解析度
    public Vector2Int GetSuggestedResolution(VolumetricUpdate obj, Camera mainCamera, Vector2Int baseResolution)
    {
        // 直接使用物件的 SizeRatio
        float sizeRatio = obj.SizeRatio;

        // 計算調整後的解析度
        int adjustedWidth = Mathf.RoundToInt(baseResolution.x * sizeRatio);
        int adjustedHeight = Mathf.RoundToInt(baseResolution.y * sizeRatio);

        // 確保解析度是 8 的倍數（為了更好的壓縮效率）
        adjustedWidth = Mathf.Max(8, (adjustedWidth / 8) * 8);
        adjustedHeight = Mathf.Max(8, (adjustedHeight / 8) * 8);

        return new Vector2Int(adjustedWidth, adjustedHeight);
    }

    // 修改：獲取建議的品質等級
    public int GetSuggestedQualityLevel(VolumetricUpdate obj, Camera mainCamera)
    {
        // 直接使用物件的 SizeRatio
        float sizeRatio = obj.SizeRatio;

        // 根據 SizeRatio 決定品質等級
        if (sizeRatio > 0.8f)
        {
            return 2; // 高品質
        }
        else if (sizeRatio > 0.5f)
        {
            return 1; // 中等品質
        }
        else
        {
            return 0; // 低品質
        }
    }

    // 計算兩個物體之間的距離變化
    private float CalculateDistanceDelta(Vector3 pos1, Vector3 pos2, Vector3 prevPos1, Vector3 prevPos2)
    {
        float currentDistance = Vector3.Distance(pos1, pos2);
        float previousDistance = Vector3.Distance(prevPos1, prevPos2);
        return Mathf.Abs(currentDistance - previousDistance);
    }

    // 計算頻寬成本
    private float CalculateBandwidthCost(float quality)
    {
        return ALPHA * (Mathf.Exp(N * quality) - 1);
    }

    // 計算誤差函數
    private float CalculateError(float distanceDelta, float bandwidthCost, float userBandwidth)
    {
        return distanceDelta * bandwidthCost / userBandwidth;
    }

    private Dictionary<Transform, HashSet<List<VolumetricUpdate>>> ComputeVisibility(List<List<VolumetricUpdate>> clusters)
    {
        var userCameras = FindUserCameras();
        Dictionary<Transform, HashSet<List<VolumetricUpdate>>> visibility = new Dictionary<Transform, HashSet<List<VolumetricUpdate>>>();

        foreach (var cam in userCameras)
        {
            visibility[cam] = new HashSet<List<VolumetricUpdate>>();
            foreach (var cluster in clusters)
            {
                bool isVisible = false;

                // 1. 首先檢查物理距離
                foreach (var obj in cluster)
                {
                    if (Vector3.Distance(cam.position, obj.position) <= VISIBILITY_DISTANCE)
                    {
                        isVisible = true;
                        break;
                    }
                }

                // 2. 如果物理距離檢查通過，再檢查誤差
                if (isVisible)
                {
                    // 找到群集的主相機
                    var mainCamera = cluster.FirstOrDefault(obj => obj == GraphManager.Instance.GetClusterInfos()
                        .FirstOrDefault(c => c.objects == cluster)?.mainCamera);

                    if (mainCamera != null)
                    {
                        // 計算群集中所有物體相對於主相機的距離變化
                        float maxDistanceDelta = 0f;
                        foreach (var obj in cluster)
                        {
                            if (obj == mainCamera) continue;

                            // 計算當前距離
                            float currentDistance = Vector3.Distance(obj.position, mainCamera.position);
                            // 計算上一時刻的距離
                            float previousDistance = Vector3.Distance(obj.lastPosition, mainCamera.lastPosition);
                            // 計算距離變化
                            float distanceDelta = Mathf.Abs(currentDistance - previousDistance);

                            maxDistanceDelta = Mathf.Max(maxDistanceDelta, distanceDelta);
                        }

                        // 計算群集的品質
                        float quality = 1.0f - (maxDistanceDelta / epsilon);
                        quality = Mathf.Clamp(quality, 0.1f, 1.0f);

                        // 計算頻寬成本
                        float bandwidthCost = ALPHA * (Mathf.Exp(N * quality) - 1);

                        // 計算誤差
                        float error = maxDistanceDelta * bandwidthCost / USER_BANDWIDTH;

                        // 只有當誤差小於閾值時，才將群集加入可見性集合
                        if (error <= gamma)
                        {
                            visibility[cam].Add(cluster);
                        }
                    }
                }
            }
        }
        return visibility;
    }

    private Dictionary<List<VolumetricUpdate>, float> UpdateQuality(
        Dictionary<Transform, HashSet<List<VolumetricUpdate>>> visibility,
        List<List<VolumetricUpdate>> clusters,
        float[] bandwidths,
        float epsilon)
    {
        Dictionary<List<VolumetricUpdate>, float> quality = new Dictionary<List<VolumetricUpdate>, float>();
        var userCameras = FindUserCameras();

        foreach (var cluster in clusters)
        {
            // 找到群集的主相機
            var mainCamera = cluster.FirstOrDefault(obj => obj == GraphManager.Instance.GetClusterInfos()
                .FirstOrDefault(c => c.objects == cluster)?.mainCamera);

            if (mainCamera == null)
            {
                quality[cluster] = 0.1f;
                continue;
            }

            // 計算群集中所有物體相對於主相機的距離變化
            float maxDistanceDelta = 0f;
            foreach (var obj in cluster)
            {
                if (obj == mainCamera) continue;

                // 計算當前距離
                float currentDistance = Vector3.Distance(obj.position, mainCamera.position);
                // 計算上一時刻的距離
                float previousDistance = Vector3.Distance(obj.lastPosition, mainCamera.lastPosition);
                // 計算距離變化
                float distanceDelta = Mathf.Abs(currentDistance - previousDistance);

                maxDistanceDelta = Mathf.Max(maxDistanceDelta, distanceDelta);
            }

            // 計算最小使用者頻寬
            float minBandwidth = float.MaxValue;
            foreach (var cam in userCameras)
            {
                if (visibility.ContainsKey(cam) && visibility[cam].Contains(cluster))
                {
                    float userBandwidth = bandwidths[Array.IndexOf(userCameras.ToArray(), cam)];
                    minBandwidth = Mathf.Min(minBandwidth, userBandwidth);
                }
            }

            if (minBandwidth == float.MaxValue)
            {
                quality[cluster] = 0.1f;
                continue;
            }

            // 計算初始品質值
            float initialQuality = Mathf.Clamp(1.0f - (maxDistanceDelta / epsilon), 0.1f, 1.0f);

            // 計算頻寬成本
            float bandwidthCost = ALPHA * (Mathf.Exp(N * initialQuality) - 1);

            // 計算誤差
            float error = maxDistanceDelta * bandwidthCost / minBandwidth;

            // 根據誤差調整品質
            float finalQuality = Mathf.Clamp(initialQuality * (1.0f - error), 0.1f, 1.0f);
            quality[cluster] = finalQuality;

            UnityEngine.Debug.Log($"群集品質計算：\n" +
                     $"最大距離變化: {maxDistanceDelta:F2}\n" +
                     $"初始品質: {initialQuality:F2}\n" +
                     $"頻寬成本: {bandwidthCost:F2}\n" +
                     $"最小頻寬: {minBandwidth:F2}\n" +
                     $"誤差: {error:F2}\n" +
                     $"最終品質: {finalQuality:F2}");
        }

        return quality;
    }

    void Update()
    {
        frameCount++;
        if (frameCount < UPDATE_FRAME_INTERVAL)
        {
            return;
        }
        frameCount = 0;

        // 輸出更新時間資訊
        UnityEngine.Debug.Log($"=== 圖形更新時間: {Time.time:F2} ===");

        // 更新所有 Object 節點的屬性
        foreach (var nodeObj in nodeObjects)
        {
            if (nodeObj == null) continue;
            var node = graphBuilder.GetNode(nodeObj.name);
            if (node != null)
            {
                // 存進歷史緩衝區
                node.positionHistory.Enqueue((node.position, Time.time));
                // 移除超過 historySeconds 的舊資料
                while (node.positionHistory.Count > 0 && Time.time - node.positionHistory.Peek().time > node.historySeconds)
                {
                    node.positionHistory.Dequeue();
                }
                // 每隔一秒才記錄一次 previousPosition
                if (Time.time - node.lastRecordTime >= node.recordInterval)
                {
                    node.previousPosition = node.position;
                    node.lastRecordTime = Time.time;
                }
                // 更新當前位置
                node.position = nodeObj.transform.position;
                // 你可以根據需要更新 moveDirection、speed 等屬性
            }
        }

        // 定期更新圖形
        UpdateGraph();
        IterativeRefinement(MAX_ITERATIONS, epsilon, gamma, userBandwidths);
    }

    public void IterativeRefinement(int maxIterations, float epsilon, float gamma, float[] userBandwidths = null)
    {
        frameStopwatch.Restart();  // 開始計時
        int iterationCount = 0;    // 新增：迭代計數器
        int k = 0;
        bool converged = false;
        List<List<VolumetricUpdate>> prevClusters = null;

        // 初始分群
        UpdateGraph();
        var clusters = clusterInfos.Select(c => c.objects.ToList()).ToList();

        // 取得 user cameras
        var userCameras = FindUserCameras();
        // 如果 userBandwidths 沒有傳入，則根據每個 cluster 的主相機 SizeRatio 計算
        if (userBandwidths == null || userBandwidths.Length != userCameras.Count)
        {
            userBandwidths = new float[userCameras.Count];
            for (int i = 0; i < userCameras.Count; i++)
            {
                // 尋找該 user camera 所屬的 cluster
                float q_c = 1.0f; // 預設值
                foreach (var cluster in clusterInfos)
                {
                    if (cluster.mainCamera != null && cluster.visibleUsers.Contains(userCameras[i].gameObject))
                    {
                        q_c = cluster.mainCamera.SizeRatio;
                        break;
                    }
                }
                // b(q_c) = ALPHA * (Mathf.Exp(N * q_c) - 1)
                userBandwidths[i] = ALPHA * (Mathf.Exp(N * q_c) - 1);
            }
            // 將計算結果存回成員變數
            this.userBandwidths = userBandwidths;
        }

        while (k < maxIterations && !converged)
        {
            iterationCount++;  // 增加迭代計數
            // A. 更新可見性
            var visibility = ComputeVisibility(clusters);

            // B. 計算每個群集的誤差
            Dictionary<List<VolumetricUpdate>, float> clusterErrors = new Dictionary<List<VolumetricUpdate>, float>();

            foreach (var cluster in clusters)
            {
                // 找到群集的主相機
                var mainCamera = cluster.FirstOrDefault(obj => obj == GraphManager.Instance.GetClusterInfos()
                    .FirstOrDefault(c => c.objects == cluster)?.mainCamera);

                if (mainCamera == null) continue;

                // 計算群集中所有物體相對於主相機的距離變化
                float maxDistanceDelta = 0f;
                foreach (var obj in cluster)
                {
                    if (obj == mainCamera) continue;

                    // 計算當前距離
                    float currentDistance = Vector3.Distance(obj.position, mainCamera.position);
                    // 計算上一時刻的距離
                    float previousDistance = Vector3.Distance(obj.lastPosition, mainCamera.lastPosition);
                    // 計算距離變化
                    float distanceDelta = Mathf.Abs(currentDistance - previousDistance);

                    maxDistanceDelta = Mathf.Max(maxDistanceDelta, distanceDelta);
                }

                // 計算群集的品質
                float quality = 1.0f - (maxDistanceDelta / epsilon);
                quality = Mathf.Clamp(quality, 0.1f, 1.0f);

                // 計算頻寬成本
                float bandwidthCost = ALPHA * (Mathf.Exp(N * quality) - 1);

                // 計算最小使用者頻寬
                float minBandwidth = float.MaxValue;
                for (int camIdx = 0; camIdx < userCameras.Count; camIdx++)
                {
                    var cam = userCameras[camIdx];
                    if (visibility.ContainsKey(cam) && visibility[cam].Contains(cluster))
                    {
                        float userBandwidth = userBandwidths[camIdx];
                        minBandwidth = Mathf.Min(minBandwidth, userBandwidth);
                    }
                }

                // 計算誤差
                float error = maxDistanceDelta * bandwidthCost / minBandwidth;
                clusterErrors[cluster] = error;

                UnityEngine.Debug.Log($"群集 {cluster.First().name} 的誤差計算：\n" +
                         $"最大距離變化: {maxDistanceDelta:F2}\n" +
                         $"品質: {quality:F2}\n" +
                         $"頻寬成本: {bandwidthCost:F2}\n" +
                         $"最小頻寬: {minBandwidth:F2}\n" +
                         $"誤差: {error:F2}");
            }

            // C. 根據誤差調整分群
            foreach (var cluster in clusters.ToList())
            {
                if (clusterErrors.ContainsKey(cluster) && clusterErrors[cluster] > gamma)
                {
                    // 如果誤差超過閾值，嘗試重新分群
                    var mainCamera = cluster.FirstOrDefault(obj => obj == GraphManager.Instance.GetClusterInfos()
                        .FirstOrDefault(c => c.objects == cluster)?.mainCamera);

                    if (mainCamera != null)
                    {
                        // 將誤差較大的物體分離出來
                        var objectsToRemove = cluster.Where(obj =>
                            obj != mainCamera &&
                            Vector3.Distance(obj.position, mainCamera.position) > epsilon).ToList();

                        if (objectsToRemove.Count > 0)
                        {
                            // 從原群集中移除這些物體
                            foreach (var obj in objectsToRemove)
                            {
                                cluster.Remove(obj);
                            }

                            // 為這些物體創建新的群集
                            var newCluster = new List<VolumetricUpdate>(objectsToRemove);
                            clusters.Add(newCluster);
                        }
                    }
                }
            }

            // D. 檢查收斂
            if (prevClusters != null && IsConverged(prevClusters, clusters))
            {
                converged = true;
            }

            prevClusters = clusters.Select(c => c.ToList()).ToList();
            k++;
            // 呼叫 UpdateQuality
            UpdateQuality(visibility, clusters, userBandwidths, epsilon);

            UnityEngine.Debug.Log($"迭代 {k} 完成，群集數量: {clusters.Count}");
        }

        frameStopwatch.Stop();  // 停止計時

        // 記錄效能指標
        float frameTime = (float)frameStopwatch.Elapsed.TotalMilliseconds;
        iterationCounts.Add(iterationCount);
        frameTimes.Add(frameTime);

        // 保持樣本數量在限制範圍內
        if (iterationCounts.Count > MAX_SAMPLES)
        {
            iterationCounts.RemoveAt(0);
            frameTimes.RemoveAt(0);
        }

        // 記錄效能指標到檔案
        LogPerformanceMetrics(iterationCount, frameTime);

        // 更新最終的分群結果
        UpdateGraph();
    }

    private void LogPerformanceMetrics(int iterationCount, float frameTime)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logContent = $"\n=== 效能指標 ({timestamp}) ===\n";
        logContent += $"迭代次數: {iterationCount}\n";
        logContent += $"幀處理時間: {frameTime:F2} ms\n";

        // 計算統計數據
        if (iterationCounts.Count > 0)
        {
            float avgIterations = iterationCounts.Average();
            float maxIterations = iterationCounts.Max();
            float avgFrameTime = frameTimes.Average();
            float maxFrameTime = frameTimes.Max();

            logContent += $"\n統計數據 (最近 {iterationCounts.Count} 幀):\n";
            logContent += $"Avg. Iterations: {avgIterations:F2}\n";
            logContent += $"Max Iterations: {maxIterations:F2}\n";
            logContent += $"Avg. Overhead (ms): {avgFrameTime:F2}\n";
            logContent += $"Max Overhead (ms): {maxFrameTime:F2}\n";
        }

        // 輸出到 Debug.Log
        UnityEngine.Debug.Log(logContent);

        // 寫入檔案
        try
        {
            string directory = Path.GetDirectoryName(performanceLogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.AppendAllText(performanceLogPath, logContent);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"寫入效能指標記錄時發生錯誤: {e.Message}");
        }
    }

    private bool IsConverged(List<List<VolumetricUpdate>> prev, List<List<VolumetricUpdate>> curr)
    {
        if (prev.Count != curr.Count) return false;
        for (int i = 0; i < prev.Count; i++)
        {
            if (!prev[i].SequenceEqual(curr[i]))
                return false;
        }
        return true;
    }

    private bool IsConverged(List<List<GameObject>> prev, List<List<GameObject>> current)
    {
        if (prev == null || current == null) return false;
        if (prev.Count != current.Count) return false;

        // 檢查每個群集是否相同
        for (int i = 0; i < prev.Count; i++)
        {
            var prevCluster = prev[i];
            var currentCluster = current[i];

            if (prevCluster.Count != currentCluster.Count) return false;

            // 檢查群集中的物件是否相同
            for (int j = 0; j < prevCluster.Count; j++)
            {
                if (prevCluster[j] != currentCluster[j]) return false;
            }
        }

        return true;
    }

    private void ApplyRenderingSettings(List<ClusterInfo> finalClusters)
    {
        foreach (var cluster in finalClusters)
        {
            // 先過濾掉所有異常物件
            int beforeCount = cluster.objects.Count;
            cluster.objects.RemoveAll(obj =>
            {
                if (obj == null)
                {
                    UnityEngine.Debug.LogWarning($"[ApplyRenderingSettings] Cluster {cluster.clusterId} 有 null 物件(預先移除)");
                    return true;
                }
                if (obj.OBJ_Pos == null)
                {
                    UnityEngine.Debug.LogWarning($"[ApplyRenderingSettings] VolumetricUpdate {(obj.name ?? "<no name>")} (Cluster {cluster.clusterId}) 的 OBJ_Pos 為 null(預先移除)");
                    return true;
                }
                return false;
            });
            int afterCount = cluster.objects.Count;
            if (beforeCount != afterCount)
            {
                UnityEngine.Debug.LogWarning($"[ApplyRenderingSettings] Cluster {cluster.clusterId} 預先移除異常物件數: {beforeCount - afterCount}");
            }

            foreach (var obj in cluster.objects)
            {
                // 使用 SizeRatio 來決定 LOD 層級
                float sizeRatio = obj.SizeRatio;

                // 調整 LOD
                LODGroup lodGroup = obj.OBJ_Pos.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    // 根據 SizeRatio 決定 LOD 層級
                    int lodLevel;
                    if (sizeRatio > 0.8f)
                    {
                        lodLevel = 0; // 最高品質
                    }
                    else if (sizeRatio > 0.5f)
                    {
                        lodLevel = 1; // 中等品質
                    }
                    else
                    {
                        lodLevel = 2; // 最低品質
                    }
                    lodGroup.ForceLOD(lodLevel);

                    UnityEngine.Debug.Log($"物件 {obj.name} LOD 調整：SizeRatio={sizeRatio:F2}, LOD Level={lodLevel}");
                }
            }
        }
    }
}