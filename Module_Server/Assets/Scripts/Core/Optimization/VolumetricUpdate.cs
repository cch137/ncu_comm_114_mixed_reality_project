using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VolumetricUpdate  // 修正：繼承 MonoBehaviour 以解決 CS0311
{
    public const int NOISE = -1;
    public const int UNCLASSIFIED = 0;
    public GameObject OBJ_Pos; // 修正：設為 public 以解決 CS0122
    private GameObject OBJ_True;
    public List<string> combinelayer = new List<string>();
    public int ClusterId;
    public string name;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 original_offset;
    public float distance_camerawithobject;
    private Vector3 new_localscale;
    private Vector3 old_localscale;
    private BoxCollider boxcollider;
    private int _time = 0;
    private Vector3[] BoundPoints;
    private float camera_orth_size;
    private float[] ScreenPos_x;
    private float[] ScreenPos_y;
    private Vector2 ObjectOnFrameSize;
    public float Onframesize;
    private Vector2 originalsize;
    private Vector2 originalpos;
    public float SizeRatio { get; private set; }  // 改為公開屬性，但只有類別內部可以設置值
    public Rect Rect_onframe; // 使用 UnityEngine.Rect
    public int layermask;  // 改為 public
    public int firstclose;
    private float Scaleratio = 1;
    public bool camera_change = false;

    // 添加速度相關字段
    public float speed;
    public Vector3 velocity;
    public Vector3 lastPosition;  // 改為 public
    private float lastUpdateTime;

    public override string ToString()
    {
        return String.Format("({0}, {1}, {2}, {3})", position.x, position.y, position.z, name);
    }

    private static float DistanceSquared(VolumetricUpdate p1, VolumetricUpdate p2)
    {
        float diffX = p2.position.x - p1.position.x;
        float diffY = p2.position.y - p1.position.y;
        float diffZ = p2.position.z - p1.position.z;
        return diffX * diffX + diffY * diffY + diffZ * diffZ;
    }

    // 以距離3公尺內分群的grouping方法
    public static void GroupByDistance(List<VolumetricUpdate> points, float distanceThreshold = 1.0f)
    {
        int clusterId = 1;

        // 先將所有物件設為未分類
        foreach (var p in points)
        {
            p.ClusterId = UNCLASSIFIED;
        }

        // 遍歷所有物件
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];

            // 如果這個物件還沒被分類
            if (p.ClusterId == UNCLASSIFIED)
            {
                // 將這個物件設為新群集
                p.ClusterId = clusterId;

                // 檢查其他所有物件
                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j) continue;  // 跳過自己

                    var q = points[j];
                    // 如果另一個物件還沒被分類，且距離在閾值內
                    if (q.ClusterId == UNCLASSIFIED &&
                        Vector3.Distance(p.position, q.position) <= distanceThreshold)
                    {
                        // 將它加入同一群
                        q.ClusterId = clusterId;
                    }
                }

                // 處理完這個群集，準備下一個群集
                clusterId++;
            }
        }

        // 輸出分群結果
        Debug.Log("=== 分群結果 ===");
        // 收集每個群集的物件
        Dictionary<int, List<string>> clusterResults = new Dictionary<int, List<string>>();
        foreach (var p in points)
        {
            if (!clusterResults.ContainsKey(p.ClusterId))
            {
                clusterResults[p.ClusterId] = new List<string>();
            }
            clusterResults[p.ClusterId].Add(p.name);
        }

        // 輸出每個群集的內容
        foreach (var cluster in clusterResults)
        {
            if (cluster.Key == NOISE)
            {
                Debug.Log($"雜訊點: {string.Join(", ", cluster.Value)}");
            }
            else
            {
                Debug.Log($"群集 {cluster.Key}: {string.Join(", ", cluster.Value)}");
            }
        }
        Debug.Log("===============");
    }

    public void object_init(GameObject gameObject)
    {
        OBJ_Pos = gameObject;
        name = gameObject.transform.parent.gameObject.name;
        layermask = gameObject.transform.parent.gameObject.layer;
        OBJ_True = gameObject;
        original_offset = OBJ_True.transform.eulerAngles;
        position = gameObject.transform.position;
        rotation = OBJ_True.transform.rotation;
        firstclose = 0;

        boxcollider = OBJ_True.GetComponent<BoxCollider>();
        BoundPoints = new Vector3[8];
        ScreenPos_x = new float[8];
        ScreenPos_y = new float[8];
        ObjectOnFrameSize = new Vector2();

        BoundPoints[0] = boxcollider.bounds.min;
        BoundPoints[7] = boxcollider.bounds.max;
        BoundPoints[1] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[0].z);
        BoundPoints[2] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[7].z);
        BoundPoints[3] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[0].z);
        BoundPoints[4] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[7].z);
        BoundPoints[5] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[0].z);
        BoundPoints[6] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[7].z);

        lastPosition = position;
        lastUpdateTime = Time.time;
        speed = 0f;
        velocity = Vector3.zero;
    }

    public void Object_update(ObjectTransform new_object_value)
    {
        ClusterId = UNCLASSIFIED;
        Vector3 Obj_position = new Vector3(new_object_value.PosX, new_object_value.PosY, new_object_value.PosZ);
        Quaternion Obj_rotation = new Quaternion(new_object_value.RotX, new_object_value.RotY, new_object_value.RotZ, new_object_value.RotW);

        OBJ_Pos.transform.position = Obj_position;
        position = OBJ_Pos.transform.position;
        OBJ_True.transform.rotation = Obj_rotation;
        rotation = OBJ_True.transform.rotation;

        float deltaTime = Time.time - lastUpdateTime;
        if (deltaTime > 0)
        {
            velocity = (position - lastPosition) / deltaTime;
            speed = velocity.magnitude;
            lastPosition = position;
            lastUpdateTime = Time.time;
        }

        if (_time == 0)
        {
            old_localscale = new Vector3(new_object_value.ScaleX, new_object_value.ScaleY, new_object_value.ScaleZ);
            _time += 1;
        }
        else
        {
            new_localscale = new Vector3(new_object_value.ScaleX, new_object_value.ScaleY, new_object_value.ScaleZ);
            if (new_localscale != old_localscale)
            {
                camera_change = true;
                Scaleratio = new_localscale.x / old_localscale.x;
                OBJ_True.transform.localScale = OBJ_True.transform.localScale * Scaleratio;
            }
            else
            {
                Scaleratio = 1;
                camera_change = false;
                Debug.Log($"camera_change: {camera_change}");
            }
            old_localscale = new_localscale;
        }

        BoundPoints[0] = boxcollider.bounds.min;
        BoundPoints[7] = boxcollider.bounds.max;
        BoundPoints[1] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[0].z);
        BoundPoints[2] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[7].z);
        BoundPoints[3] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[0].z);
        BoundPoints[4] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[7].z);
        BoundPoints[5] = new Vector3(BoundPoints[7].x, BoundPoints[7].y, BoundPoints[0].z);
        BoundPoints[6] = new Vector3(BoundPoints[7].x, BoundPoints[0].y, BoundPoints[7].z);
    }

    public float GetScaleratio()
    {
        return Scaleratio;
    }

    public bool GetCamera_change()
    {
        return camera_change;
    }

    public void originalsize_init(Camera objectstreamingCam)
    {
        originalsize = new Vector2();
        originalpos = new Vector2();
        originalsize.x = objectstreamingCam.rect.width;
        originalsize.y = objectstreamingCam.rect.height;
        originalpos.x = objectstreamingCam.rect.x;
        originalpos.y = objectstreamingCam.rect.y;
    }

    public bool Visiable(Camera maincamera, Camera objectstreamingCam)
    {
        if (objectstreamingCam.cullingMask != 8)
        {
            firstclose = 0;
            camera_orth_size = objectstreamingCam.orthographicSize;
        }

        if (IsObjectInFrustum(maincamera, BoundPoints))
        {
            Debug.Log($"Object {name} is visible");
            objectstreamingCam.orthographicSize = camera_orth_size;
            CaculateObjectOnViewSize(maincamera, BoundPoints);
            objectstreamingCam.rect = new Rect(originalpos.x, originalpos.y, originalsize.x * SizeRatio, originalsize.y * SizeRatio);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool VisiableWithGraph(Camera maincamera, Camera objectstreamingCam)
    {
        if (objectstreamingCam.cullingMask != 8)
        {
            firstclose = 0;
            camera_orth_size = objectstreamingCam.orthographicSize;
        }

        // 檢查是否在主相機的群組中
        bool isInMainCameraCluster = false;
        float clusterWeight = 1.0f;
        float userClusterWeight = 1.0f;

        if (GraphManager.Instance != null)
        {
            var clusters = GraphManager.Instance.GetClusterInfos();
            var cluster = clusters.FirstOrDefault(c => c.objects.Contains(this));

            if (cluster != null && cluster.mainCamera != null)
            {
                isInMainCameraCluster = (cluster.mainCamera == this);

                // 獲取使用者與群集的權重
                userClusterWeight = GraphManager.Instance.GetUserClusterWeight(maincamera.gameObject, cluster.mainCamera);

                // 獲取群集內部的權重
                clusterWeight = GraphManager.Instance.GetClusterWeight(cluster.mainCamera);
            }
        }

        // 計算可見性閾值
        float visibilityThreshold = 1.0f;
        if (GraphManager.Instance != null)
        {
            // 權重越小，閾值越低（更容易可見）
            visibilityThreshold = Mathf.Clamp(userClusterWeight * clusterWeight, 0.5f, 2.0f);
        }

        // 檢查是否在視錐體內
        bool isInFrustum = IsObjectInFrustum(maincamera, BoundPoints);

        // 如果是主相機或權重較小（距離較近），降低可見性要求
        if (isInMainCameraCluster || visibilityThreshold < 1.0f)
        {
            if (isInFrustum)
            {
                Debug.Log($"Object {name} is visible (Main camera or close cluster)");
                objectstreamingCam.orthographicSize = camera_orth_size;
                CaculateObjectOnViewSize(maincamera, BoundPoints);
                objectstreamingCam.rect = new Rect(originalpos.x, originalpos.y, originalsize.x * SizeRatio, originalsize.y * SizeRatio);
                return true;
            }
        }
        else
        {
            // 對於非主相機且權重較大的物件，需要更嚴格的可見性檢查
            if (isInFrustum && visibilityThreshold < 1.5f)
            {
                Debug.Log($"Object {name} is visible (Normal check with weight: {visibilityThreshold})");
                objectstreamingCam.orthographicSize = camera_orth_size;
                CaculateObjectOnViewSize(maincamera, BoundPoints);
                objectstreamingCam.rect = new Rect(originalpos.x, originalpos.y, originalsize.x * SizeRatio, originalsize.y * SizeRatio);
                return true;
            }
        }

        return false;
    }

    static bool IsPointInFrustum(Camera maincamera, Vector3 point)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(maincamera);
        for (int i = 0, iMax = planes.Length; i < iMax; ++i)
        {
            if (planes[i].GetSide(point))
            {
                return false;
            }
        }
        return true;
    }

    static bool IsObjectInFrustum(Camera maincamera, Vector3[] Boundpoints)
    {
        int count = 0;
        for (int i = 0; i < Boundpoints.Length; i++)
        {
            if (IsPointInFrustum(maincamera, Boundpoints[i]))
            {
                count += 1;
            }
            if (count >= 1)
            {
                return true;
            }
        }
        return false;
    }

    private void CaculateObjectOnViewSize(Camera maincamera, Vector3[] Boundpoints)
    {
        for (int i = 0; i < BoundPoints.Length; i++)
        {
            Vector3 screenPos = maincamera.WorldToScreenPoint(Boundpoints[i]);
            ScreenPos_x[i] = screenPos.x;
            ScreenPos_y[i] = screenPos.y;
        }

        double diff_x = ScreenPos_x.Max() - ScreenPos_x.Min();
        double diff_y = ScreenPos_y.Max() - ScreenPos_y.Min();
        Onframesize = (float)(diff_x * diff_y);
        Rect_onframe = new Rect((int)ScreenPos_x.Min(), (int)ScreenPos_y.Min(), (int)diff_x, (int)diff_y);

        ObjectOnFrameSize.x = (float)Math.Floor(diff_x / 96) / 10;
        ObjectOnFrameSize.y = (float)Math.Floor(diff_y / 96) / 10;

        if (ObjectOnFrameSize.x > 1)
        {
            ObjectOnFrameSize.x = 1.0f;
        }
        if (ObjectOnFrameSize.y > 1)
        {
            ObjectOnFrameSize.y = 1.0f;
        }

        // 計算基礎 SizeRatio
        float baseSizeRatio = (ObjectOnFrameSize.x + ObjectOnFrameSize.y) / 2;

        // 獲取圖形權重調整
        float weightAdjustment = 1.0f;
        if (GraphManager.Instance != null)
        {
            // 找到這個物件所屬的群集
            var clusters = GraphManager.Instance.GetClusterInfos();
            var cluster = clusters.FirstOrDefault(c => c.objects.Contains(this));

            if (cluster != null && cluster.mainCamera != null)
            {
                // 獲取使用者與群集的權重
                float userClusterWeight = GraphManager.Instance.GetUserClusterWeight(maincamera.gameObject, cluster.mainCamera);

                // 獲取群集內部的權重
                float clusterWeight = GraphManager.Instance.GetClusterWeight(cluster.mainCamera);

                // 計算總權重
                float totalWeight = userClusterWeight * clusterWeight;

                // 根據權重範圍決定調整因子
                if (totalWeight <= 5.0f)
                {
                    weightAdjustment = 1.0f;  // 最高品質
                }
                else if (totalWeight <= 10.0f)
                {
                    weightAdjustment = 0.8f;  // 高品質
                }
                else if (totalWeight <= 20.0f)
                {
                    weightAdjustment = 0.6f;  // 中等品質
                }
                else if (totalWeight <= 30.0f)
                {
                    weightAdjustment = 0.4f;  // 低品質
                }
                else
                {
                    weightAdjustment = 0.3f;  // 最低品質
                }

                Debug.Log(
                    $"物件 {name} 的權重資訊：\n" +
                    $"使用者群集權重: {userClusterWeight:F2}\n" +
                    $"群集內部權重: {clusterWeight:F2}\n" +
                    $"總權重: {totalWeight:F2}\n" +
                    $"權重調整因子: {weightAdjustment:F2}"
                );
            }
        }

        // 應用權重調整
        SizeRatio = baseSizeRatio * weightAdjustment;

        // 確保 SizeRatio 在 0.3 到 1.0 的範圍內
        SizeRatio = Mathf.Clamp(SizeRatio, 0.3f, 1.0f);

        Debug.Log(
            $"物件 {name} 的 SizeRatio 計算：\n" +
            $"基礎 SizeRatio: {baseSizeRatio:F2}\n" +
            $"最終 SizeRatio: {SizeRatio:F2}"
        );

        // 解析度調整
        if (OBJ_Pos != null)
        {
            var cam = OBJ_Pos.GetComponent<Camera>();
            if (cam != null)
            {
                int baseWidth = 1920;
                int baseHeight = 1080;
                int targetWidth = Mathf.RoundToInt(baseWidth * SizeRatio);
                int targetHeight = Mathf.RoundToInt(baseHeight * SizeRatio);
                targetWidth = Mathf.Max(8, (targetWidth / 8) * 8);
                targetHeight = Mathf.Max(8, (targetHeight / 8) * 8);
                cam.targetTexture = new RenderTexture(targetWidth, targetHeight, 24);
                Debug.Log($"物件 {name} 解析度調整：SizeRatio={SizeRatio:F2}, 解析度={targetWidth}x{targetHeight}");
            }
        }
    }

    private double GetEvenNum(double num)
    {
        if (num % 2 == 0)
        {
            return num;
        }
        else
        {
            return num + 1;
        }
    }

    // 群集資訊類別
    public class ClusterInfo
    {
        public int clusterId;
        public List<VolumetricUpdate> objects;
        public VolumetricUpdate mainCamera;  // 負責拍攝的物件
        public List<int> targetLayers;        // 需要拍攝的層級

        public ClusterInfo(int id)
        {
            clusterId = id;
            objects = new List<VolumetricUpdate>();
            targetLayers = new List<int>();
        }
    }

    // 靜態列表，記錄所有群集資訊
    public static List<ClusterInfo> clusterInfos = new List<ClusterInfo>();

    // 更新群集資訊
    public static void UpdateClusterInfo(List<VolumetricUpdate> points)
    {
        // 清除舊的群集資訊
        clusterInfos.Clear();

        // 先進行分群
        GroupByDistance(points);

        // 收集每個群集的物件
        Dictionary<int, List<VolumetricUpdate>> clusterGroups = new Dictionary<int, List<VolumetricUpdate>>();
        foreach (var point in points)
        {
            if (point.ClusterId > 0)  // 排除雜訊點
            {
                if (!clusterGroups.ContainsKey(point.ClusterId))
                {
                    clusterGroups[point.ClusterId] = new List<VolumetricUpdate>();
                }
                clusterGroups[point.ClusterId].Add(point);
            }
        }

        // 為每個群集創建資訊並決定主相機
        foreach (var group in clusterGroups)
        {
            ClusterInfo clusterInfo = new ClusterInfo(group.Key);
            clusterInfo.objects = group.Value;

            // 決定主相機（這裡使用最接近群集中心的物件）
            Vector3 center = Vector3.zero;
            foreach (var obj in group.Value)
            {
                center += obj.position;
            }
            center /= group.Value.Count;

            // 找出最接近中心的物件作為主相機
            float minDist = float.MaxValue;
            foreach (var obj in group.Value)
            {
                float dist = Vector3.Distance(obj.position, center);
                if (dist < minDist)
                {
                    minDist = dist;
                    clusterInfo.mainCamera = obj;
                }
            }

            // 收集需要拍攝的層級
            foreach (var obj in group.Value)
            {
                if (obj != clusterInfo.mainCamera)
                {
                    clusterInfo.targetLayers.Add(obj.layermask);
                }
            }

            clusterInfos.Add(clusterInfo);
        }
    }

    // 獲取群集資訊的方法（供其他腳本使用）
    public static List<ClusterInfo> GetClusterInfos()
    {
        return clusterInfos;
    }


}