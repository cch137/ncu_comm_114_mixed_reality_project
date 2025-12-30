using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;  // 新增：用於 Stopwatch

public class GraphBuilder
{
    public class Node
    {
        public string id;
        public Vector3 position;
        public Vector3 previousPosition;  // 追蹤前一個時間點的位置
        public float viewDirection;  // 使用角度制
        public bool isUser;
        public float lastUpdateTime;  // 每個節點獨立的更新時間
        public float previousPositionTime;  // 記錄前一個位置的時間
        public Vector3 tempPosition;  // 臨時保存前一刻的位置
        public float moveDirection;  // 新增：記錄物件的移動方向
        public float speed;  // 新增：記錄物件的移動速度

        // 新增：記錄位置和時間的歷史
        public Queue<(Vector3 pos, float time)> positionHistory = new Queue<(Vector3, float)>();
        public float historySeconds = 60f; // 保留60秒內的歷史
        // 新增：每隔一段時間才記錄一次 previousPosition
        public float lastRecordTime = 0f;
        public float recordInterval = 1.0f; // 每1秒記錄一次

        public Node(string id, Vector3 position, float viewDirection, float moveDirection, float speed, bool isUser)
        {
            this.id = id;
            this.position = position;
            this.previousPosition = position;
            this.tempPosition = position;
            this.viewDirection = viewDirection;
            this.moveDirection = moveDirection;
            this.speed = speed;
            this.isUser = isUser;
            this.lastUpdateTime = Time.time;
            this.previousPositionTime = Time.time;
            this.lastRecordTime = Time.time;
        }

        // 取得 N 秒前的位置
        public Vector3 GetPositionSecondsAgo(float secondsAgo)
        {
            float targetTime = Time.time - secondsAgo;
            Vector3 closest = position;
            float minDiff = float.MaxValue;
            foreach (var (pos, t) in positionHistory)
            {
                float diff = Mathf.Abs(t - targetTime);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = pos;
                }
            }
            return closest;
        }
    }

    public class Edge
    {
        public Node source;
        public Node target;
        public float weight;

        public Edge(Node source, Node target, float weight)
        {
            this.source = source;
            this.target = target;
            this.weight = weight;
        }
    }

    private Dictionary<string, Node> nodes = new Dictionary<string, Node>();
    private List<Edge> edges = new List<Edge>();

    // 新增：效能測量相關變數
    private Stopwatch buildGraphStopwatch = new Stopwatch();
    private List<float> buildGraphTimes = new List<float>();
    private const int MAX_SAMPLES = 100;  // 最多保存100個樣本

    // [說明] 本系統所有高耗能優化流程（建構圖、叢集、品質）皆以時間為主的節流方式（100ms）統一控制。
    public void AddUserNode(string id, Vector3 position, float viewDirection)
    {
        nodes[id] = new Node(id, position, viewDirection, 0f, 0f, true);
    }

    public void AddObjectNode(string id, Vector3 position, float moveDirection, float speed)
    {
        // 檢查速度和方向是否有效
        if (float.IsNaN(speed) || float.IsInfinity(speed))
        {
            UnityEngine.Debug.LogWarning($"物件 {id} 的速度無效: {speed}，使用預設值 0");
            speed = 0f;
        }

        if (float.IsNaN(moveDirection) || float.IsInfinity(moveDirection))
        {
            UnityEngine.Debug.LogWarning($"物件 {id} 的方向無效: {moveDirection}，使用預設值 0");
            moveDirection = 0f;
        }

        // 確保方向在 0-360 度範圍內
        moveDirection = moveDirection % 360f;
        if (moveDirection < 0)
        {
            moveDirection += 360f;
        }

        float currentTime = Time.time;

        if (nodes.ContainsKey(id))
        {
            var node = nodes[id];
            node.position = position;
            node.moveDirection = moveDirection;
            node.speed = speed;
            node.lastUpdateTime = currentTime;
        }
        else
        {
            nodes[id] = new Node(id, position, 0f, moveDirection, speed, false);
            UnityEngine.Debug.Log($"新增物件 {id}，初始位置: {position}");
        }
    }

    private float CalculateDistance(Vector3 pos1, Vector3 pos2)
    {
        float distance = Vector3.Distance(pos1, pos2);
        if (float.IsNaN(distance) || float.IsInfinity(distance))
        {
            UnityEngine.Debug.LogWarning($"計算距離時出現無效值 - 位置1: {pos1}, 位置2: {pos2}");
            return 1.0f; // 返回預設值
        }
        return distance;
    }


    private float CalculateViewAngle(Vector3 viewerPos, Vector3 targetPos)
    {
        Vector3 direction = targetPos - viewerPos;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (float.IsNaN(angle) || float.IsInfinity(angle))
        {
            UnityEngine.Debug.LogWarning($"計算視角時出現無效值 - 觀察者位置: {viewerPos}, 目標位置: {targetPos}");
            return 0f; // 返回預設值
        }
        return angle;
    }

    public void BuildGraph()
    {
        buildGraphStopwatch.Restart();  // 開始計時
        UnityEngine.Debug.Log("開始建立圖形...");
        
        // 清除現有的邊
        edges.Clear();

        // 獲取所有使用者節點和物件節點
        var users = nodes.Values.Where(n => n.isUser).ToList();
        var objects = nodes.Values.Where(n => !n.isUser).ToList();

        UnityEngine.Debug.Log($"當前節點數量 - 使用者: {users.Count}, 物件: {objects.Count}");

        // 檢查是否有物件節點
        if (objects.Count == 0)
        {
            UnityEngine.Debug.LogWarning("沒有物件節點可供建立圖形");
            return;
        }

        // 建立使用者到物件的邊
        foreach (var user in users)
        {
            foreach (var obj in objects)
            {
                float distance = CalculateDistance(user.position, obj.position);
                float viewAngle = CalculateViewAngle(user.position, obj.position);
                
                // 計算權重，添加保護機制
                float weight = distance * (1f + 0.5f * (1f - Mathf.Cos(viewAngle * Mathf.Deg2Rad)));
                
                // 檢查權重是否為有效值
                if (float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    UnityEngine.Debug.LogWarning($"計算權重時出現無效值 - 使用者: {user.id}, 物件: {obj.id}, 距離: {distance}, 角度差: {viewAngle}");
                    weight = distance; // 使用距離作為備用權重
                }
                
                edges.Add(new Edge(user, obj, weight));
            }
        }

        // 建立物件之間的邊
        for (int i = 0; i < objects.Count; i++)
        {
            for (int j = i + 1; j < objects.Count; j++)
            {
                var obj1 = objects[i];
                var obj2 = objects[j];

                UnityEngine.Debug.Log($"處理物件對 - 物件1: {obj1.id}, 物件2: {obj2.id}\n" +
                         $"物件1資訊：\n" +
                         $"  當前位置: {obj1.position}\n" +
                         $"  前一個位置: {obj1.previousPosition}\n" +
                         $"  最後更新時間: {obj1.lastUpdateTime}\n" +
                         $"物件2資訊：\n" +
                         $"  當前位置: {obj2.position}\n" +
                         $"  前一個位置: {obj2.previousPosition}\n" +
                         $"  最後更新時間: {obj2.lastUpdateTime}");

                // 計算當前距離
                float currentDistance = CalculateDistance(obj1.position, obj2.position);
                
                // 計算前一個時間點的距離
                float previousDistance = CalculateDistance(obj1.previousPosition, obj2.previousPosition);
                
                // 計算距離變化
                float distanceChange = Mathf.Abs(currentDistance - previousDistance);
                
                // 使用 λ = 1.0 的權重參數
                float lambda = 1.0f;
                
                // 計算最終權重：距離 + 距離變化 + 相對運動不穩定性
                float weight = currentDistance + lambda * distanceChange;

                // 輸出詳細的除錯訊息
                UnityEngine.Debug.Log($"物件對物件權重計算 - 物件1: {obj1.id}, 物件2: {obj2.id}\n" +
                         $"物件1當前位置: {obj1.position}, 前一個位置: {obj1.previousPosition}\n" +
                         $"物件2當前位置: {obj2.position}, 前一個位置: {obj2.previousPosition}\n" +
                         $"當前距離: {currentDistance:F2}, 前一個距離: {previousDistance:F2}\n" +
                         $"距離變化: {distanceChange:F2}\n" +
                         $"最終權重: {weight:F2}");
                
                // 檢查權重是否為有效值
                if (float.IsNaN(weight) || float.IsInfinity(weight))
                {
                    UnityEngine.Debug.LogWarning($"計算權重時出現無效值 - 物件1: {obj1.id}, 物件2: {obj2.id}, 當前距離: {currentDistance}, 距離變化: {distanceChange}");
                    weight = currentDistance; // 使用當前距離作為備用權重
                }
                
                edges.Add(new Edge(obj1, obj2, weight));
            }
        }

        UnityEngine.Debug.Log($"圖形建立完成 - 總邊數: {edges.Count}");
        buildGraphStopwatch.Stop();  // 停止計時

        // 記錄建圖時間
        float buildTime = (float)buildGraphStopwatch.Elapsed.TotalMilliseconds;
        buildGraphTimes.Add(buildTime);

        // 保持樣本數量在限制範圍內
        if (buildGraphTimes.Count > MAX_SAMPLES)
        {
            buildGraphTimes.RemoveAt(0);
        }

        // 輸出建圖時間統計
        if (buildGraphTimes.Count > 0)
        {
            float avgBuildTime = buildGraphTimes.Average();
            float maxBuildTime = buildGraphTimes.Max();
            float minBuildTime = buildGraphTimes.Min();

            UnityEngine.Debug.Log($"建圖效能統計 (最近 {buildGraphTimes.Count} 次):\n" +
                     $"平均建圖時間: {avgBuildTime:F2} ms\n" +
                     $"最大建圖時間: {maxBuildTime:F2} ms\n" +
                     $"最小建圖時間: {minBuildTime:F2} ms");
        }
    }

    public List<Edge> GetEdges()
    {
        return edges;
    }

    public Dictionary<string, Node> GetNodes()
    {
        return nodes;
    }

    public Node GetNode(string id)
    {
        if (nodes.ContainsKey(id))
            return nodes[id];
        return null;
    }
} 