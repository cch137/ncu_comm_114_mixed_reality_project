using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ObjectManager : MonoBehaviour
{
    public float bandwidthMax = 50f;
    public float epsilonError = 0.1f;
    public float smallSize = 0.25f;
    public float largeSize = 1.0f;
    public float speedThreshold = 0.2f;
    public float maxSpeed = 1.0f;
    public GameObject usersParent; // 在Inspector中拖入Users GameObject

    private List<GameObject> nodes;
    private List<List<GameObject>> clusters;
    private HashSet<GameObject> visited;
    private Dictionary<GameObject, List<GameObject>> graph;
    private List<Camera> userCameras; // 儲存所有使用者的User_Position攝影機

    void Start()
    {
        nodes = new List<GameObject>();
        clusters = new List<List<GameObject>>();
        visited = new HashSet<GameObject>();
        graph = new Dictionary<GameObject, List<GameObject>>();
        userCameras = new List<Camera>();

        InitializeNodes();
        FindUserCameras();
        SetupColliders();
        BuildGraphAndClusters();
        OptimizeStreaming();
    }

    // 查找Users下的所有EXP_20實例的User_Position攝影機
    void FindUserCameras()
    {
        if (usersParent == null)
        {
            Debug.LogError("Users parent GameObject not assigned!");
            return;
        }

        foreach (Transform user in usersParent.transform)
        {
            Transform controlCenter = user.Find("Control_center");
            if (controlCenter != null)
            {
                Transform userPosition = controlCenter.Find("User_Position");
                if (userPosition != null)
                {
                    Camera cam = userPosition.GetComponent<Camera>();
                    if (cam != null)
                    {
                        userCameras.Add(cam);
                        Debug.Log($"Found camera for user: {user.name} at {userPosition.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"No Camera component found in {userPosition.name} for user {user.name}");
                    }
                }
                else
                {
                    Debug.LogWarning($"User_Position not found under Control_center for user: {user.name}");
                }
            }
            else
            {
                Debug.LogWarning($"Control_center not found for user: {user.name}");
            }
        }
    }

    void InitializeNodes()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Object") && child.childCount > 0)
            {
                GameObject node = child.GetChild(0).gameObject;
                nodes.Add(node);
                if (!node.GetComponent<Rigidbody>())
                    node.AddComponent<Rigidbody>();
                if (!node.GetComponent<Renderer>())
                    Debug.LogWarning($"{node.name} lacks a Renderer, bounds may be inaccurate.");
            }
        }
        Debug.Log($"Initialized {nodes.Count} nodes (first children of 'Object' prefixed objects).");
    }

    void SetupColliders()
    {
        foreach (var node in nodes)
        {
            if (node == null) continue;
            float size = node.GetComponent<Renderer>()?.bounds.size.magnitude ?? 1f;
            Vector3 vel = node.GetComponent<Rigidbody>().velocity;
            float speed = vel.magnitude;

            Collider existingCollider = node.GetComponent<Collider>();
            if (existingCollider != null && speed > maxSpeed)
            {
                Destroy(existingCollider); // 僅在速度過快時移除
                continue;
            }

            if (existingCollider == null || speed != 0) // 僅在需要時重建
            {
                Destroy(node.GetComponent<Collider>()); // 移除舊的Collider

                if (speed < speedThreshold)
                {
                    SphereCollider sphere = node.AddComponent<SphereCollider>();
                    sphere.radius = (size <= smallSize) ? smallSize / 2f : largeSize / 2f;
                    sphere.center = node.GetComponent<Renderer>()?.bounds.center - node.transform.position ?? Vector3.zero;
                    sphere.isTrigger = true;
                }
                else if (speed <= maxSpeed)
                {
                    BoxCollider box = node.AddComponent<BoxCollider>();
                    Vector3 direction = vel.normalized;
                    box.size = new Vector3(size / 2f, size / 2f, size + speed);
                    box.center = node.GetComponent<Renderer>()?.bounds.center - node.transform.position ?? Vector3.zero;
                    node.transform.rotation = Quaternion.LookRotation(direction);
                    box.isTrigger = true;
                }
            }
            else
            {
                // 保持現有Collider（靜止且已正確設置）
                if (existingCollider is SphereCollider sphere)
                {
                    sphere.radius = (size <= smallSize) ? smallSize / 2f : largeSize / 2f;
                    sphere.center = node.GetComponent<Renderer>()?.bounds.center - node.transform.position ?? Vector3.zero;
                    sphere.isTrigger = true;
                }
                else if (existingCollider is BoxCollider box)
                {
                    float sizeNow = box.GetComponent<Renderer>()?.bounds.size.magnitude ?? 1f;
                    Vector3 direction = vel.normalized;
                    box.size = new Vector3(sizeNow / 2f, sizeNow / 2f, sizeNow + speed);
                    box.center = node.GetComponent<Renderer>()?.bounds.center - node.transform.position ?? Vector3.zero;
                    node.transform.rotation = Quaternion.LookRotation(direction);
                    box.isTrigger = true;
                }
            }
        }
    }

    void BuildGraphAndClusters()
    {
        graph.Clear();
        foreach (var node in nodes)
            if (node != null) graph[node] = new List<GameObject>();

        visited.Clear();
        clusters.Clear();

        foreach (var node in nodes)
        {
            if (node == null || visited.Contains(node) || !node.GetComponent<Collider>()) continue;
            visited.Add(node);
            List<GameObject> neighbors = GetColliderNeighbors(node);
            if (neighbors.Count > 0)
            {
                graph[node].AddRange(neighbors); // 構建Graph邊
                List<GameObject> cluster = new List<GameObject>();
                ExpandCluster(node, cluster);
                clusters.Add(cluster);
            }
            else
            {
                graph[node] = new List<GameObject>(); // 確保每個Node在Graph中
            }
        }

        // 處理無Collider的獨立Node
        foreach (var node in nodes)
        {
            if (node != null && !node.GetComponent<Collider>() && !visited.Contains(node))
            {
                clusters.Add(new List<GameObject> { node });
                visited.Add(node);
                if (!graph.ContainsKey(node)) graph[node] = new List<GameObject>(); // 確保Graph包含所有Node
            }
        }

        Debug.Log($"Graph rebuilt with {graph.Count} nodes and {graph.Sum(kvp => kvp.Value.Count)} edges.");
    }

    List<GameObject> GetColliderNeighbors(GameObject center)
    {
        List<GameObject> neighbors = new List<GameObject>();
        if (center == null) return neighbors;
        Collider centerCollider = center.GetComponent<Collider>();
        if (centerCollider == null) return neighbors;

        Collider[] hits;
        if (centerCollider is SphereCollider sphere)
            hits = Physics.OverlapSphere(center.transform.position + sphere.center, sphere.radius);
        else
        {
            BoxCollider box = centerCollider as BoxCollider;
            hits = Physics.OverlapBox(center.transform.position + box.center, box.size / 2f, center.transform.rotation);
        }

        foreach (var hit in hits)
        {
            GameObject neighbor = hit.gameObject;
            if (neighbor != center && nodes.Contains(neighbor) && neighbor.GetComponent<Collider>())
                neighbors.Add(neighbor);
        }
        return neighbors;
    }

    void ExpandCluster(GameObject point, List<GameObject> cluster)
    {
        cluster.Add(point);
        List<GameObject> neighbors = GetColliderNeighbors(point);

        foreach (var neighbor in neighbors)
        {
            if (!visited.Contains(neighbor))
            {
                visited.Add(neighbor);
                ExpandCluster(neighbor, cluster);
            }
        }
    }

    void OptimizeStreaming()
    {
        float totalBandwidth = 0f;
        List<(GameObject, float, float)> streamingParams = new List<(GameObject, float, float)>();

        foreach (var cluster in clusters)
        {
            Vector3 centroid = Vector3.zero;
            float totalSize = 0f;
            float maxVel = 0f;
            foreach (var node in cluster)
            {
                if (node == null) continue;
                centroid += node.GetComponent<Renderer>()?.bounds.center ?? node.transform.position;
                totalSize += node.GetComponent<Renderer>()?.bounds.size.magnitude ?? 1f;
                maxVel = Mathf.Max(maxVel, node.GetComponent<Rigidbody>().velocity.magnitude);
            }
            centroid /= cluster.Count;

            bool canMerge = true;
            foreach (var node in cluster)
            {
                if (node == null) continue;
                Vector3 nodeCenter = node.GetComponent<Renderer>()?.bounds.center ?? node.transform.position;
                if (Vector3.Distance(nodeCenter, centroid) > epsilonError)
                {
                    canMerge = false;
                    break;
                }
            }

            if (canMerge && cluster.Count > 1)
            {
                float importance = CalculateAverageImportance(centroid);
                float q = 0.5f + 0.5f * importance;
                float f = 30f * (1 + maxVel / 5f);
                float bandwidth = totalSize * q * f * 0.001f;
                streamingParams.Add((cluster[0], q, f));
                totalBandwidth += bandwidth;
            }
            else
            {
                foreach (var node in cluster)
                {
                    if (node == null) continue;
                    Vector3 nodeCenter = node.GetComponent<Renderer>()?.bounds.center ?? node.transform.position;
                    float importance = CalculateAverageImportance(nodeCenter);
                    float q = 0.5f + 0.5f * importance;
                    float f = 30f * (1 + node.GetComponent<Rigidbody>().velocity.magnitude / 5f);
                    float bandwidth = (node.GetComponent<Renderer>()?.bounds.size.magnitude ?? 1f) * q * f * 0.001f;
                    streamingParams.Add((node, q, f));
                    totalBandwidth += bandwidth;
                }
            }
        }

        while (totalBandwidth > bandwidthMax)
        {
            streamingParams.Sort((a, b) => CalculateAverageImportance(a.Item1?.GetComponent<Renderer>()?.bounds.center ?? a.Item1.transform.position)
                                          .CompareTo(CalculateAverageImportance(b.Item1?.GetComponent<Renderer>()?.bounds.center ?? b.Item1.transform.position)));
            var lowPriority = streamingParams[0];
            streamingParams[0] = (lowPriority.Item1, lowPriority.Item2 * 0.9f, lowPriority.Item3 * 0.9f);
            totalBandwidth = RecalculateBandwidth(streamingParams);
        }

        ApplyStreaming(streamingParams);
    }

    // 計算所有使用者的User_Position攝影機的平均重要性
    float CalculateAverageImportance(Vector3 pos)
    {
        if (userCameras == null || userCameras.Count == 0)
        {
            Debug.LogWarning("No user cameras (User_Position) found. Using default importance value.");
            return 0.5f;
        }

        float totalImportance = 0f;
        int validCameras = 0;

        foreach (var camera in userCameras)
        {
            if (camera != null)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(pos);
                totalImportance += viewportPoint.z > 0 ? 1f : 0.5f;
                validCameras++;
            }
        }

        return validCameras > 0 ? totalImportance / validCameras : 0.5f;
    }

    float RecalculateBandwidth(List<(GameObject, float, float)> parameters)
    {
        float total = 0f;
        foreach (var param in parameters)
            if (param.Item1 != null)
                total += (param.Item1.GetComponent<Renderer>()?.bounds.size.magnitude ?? 1f) * param.Item2 * param.Item3 * 0.001f;
        return total;
    }

    void ApplyStreaming(List<(GameObject, float, float)> parameters)
    {
        foreach (var param in parameters)
            if (param.Item1 != null)
                Debug.Log($"{param.Item1.name}: Q={param.Item2}, f={param.Item3}");
    }

    void OnTriggerEnter(Collider other)
    {
        if (nodes.Contains(other.gameObject))
            Debug.Log($"{other.gameObject.name} triggered with {gameObject.name}");
    }

    void Update()
    {
        if (Time.frameCount % 6 == 0)
        {
            // 移除SetupColliders()，只更新Graph和串流
            BuildGraphAndClusters();
            OptimizeStreaming();
        }
    }

    void OnDrawGizmos()
    {
        if (nodes == null || graph == null) return;

        foreach (var node in nodes)
        {
            if (node == null) continue;
            SphereCollider sphere = node.GetComponent<SphereCollider>();
            BoxCollider box = node.GetComponent<BoxCollider>();

            if (sphere != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(node.transform.position + sphere.center, sphere.radius);
            }
            else if (box != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.matrix = Matrix4x4.TRS(node.transform.position + box.center, node.transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
            }
            else if (node.GetComponent<Renderer>() != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(node.GetComponent<Renderer>().bounds.center, node.GetComponent<Renderer>().bounds.size);
            }
        }

        Gizmos.color = Color.red;
        foreach (var node in graph.Keys)
        {
            if (node == null) continue;
            Vector3 nodeCenter = node.GetComponent<Renderer>()?.bounds.center ?? node.transform.position;
            List<GameObject> neighbors = graph[node];
            if (neighbors == null) continue;
            foreach (var neighbor in neighbors)
            {
                if (neighbor == null) continue;
                Vector3 neighborCenter = neighbor.GetComponent<Renderer>()?.bounds.center ?? neighbor.transform.position;
                Gizmos.DrawLine(nodeCenter, neighborCenter);
            }
        }
    }
}