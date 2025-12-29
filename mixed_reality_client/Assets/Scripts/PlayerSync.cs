using UnityEngine;
// 移除舊的 XR 引用，避免混淆
// using UnityEngine.XR.Interaction.Toolkit; 

public class PlayerSync : MonoBehaviour
{
    [Header("追蹤物件 (沒拖曳會自動抓取)")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("同步設定")]
    [Tooltip("發送頻率 (秒)，建議 0.05 ~ 0.1")]
    public float syncRate = 0.1f;
    private float timer;

    private const int IDX_HEAD = 0;
    private const int IDX_L_HAND = 1;
    private const int IDX_R_HAND = 2;

    void Start()
    {
        AutoFindXRDevices();
    }

    void Update()
    {
        // 定期檢查 (每 3 秒一次)，防止手把後來才開機沒抓到
        if (leftHand == null || rightHand == null)
        {
            if (Time.frameCount % 180 == 0) AutoFindXRDevices();
        }

        if (NetworkManager.Instance == null) return;
        if (RoomManager.Instance == null || !RoomManager.Instance.IsInRoom) return;

        timer += Time.deltaTime;
        if (timer >= syncRate)
        {
            SendPlayerState();
            timer = 0;
        }
    }

    // ★★★ 修正後的自動抓取邏輯 (針對 XRIT 3.x) ★★★
    void AutoFindXRDevices()
    {
        // 1. 自動抓頭部 (Main Camera)
        if (head == null)
        {
            if (Camera.main != null)
            {
                head = Camera.main.transform;
                // Debug.Log("[PlayerSync] 自動綁定 Head -> MainCamera");
            }
        }

        // 2. 自動抓雙手 (透過名稱模糊搜尋)
        // 這是最穩定的方法，不依賴 XRIT 版本
        if (leftHand == null || rightHand == null)
        {
            // 搜尋場景中所有含有 "Controller" 字眼的物件
            // 為了效能，我們直接找場景中的 GameObject，這比 FindObjectsByType 快且通用

            // 這裡使用 Find 搭配常見命名規則 (Unity Default, Meta Quest, etc.)
            if (leftHand == null)
            {
                leftHand = FindHandObject("Left");
                if (leftHand != null) Debug.Log("[PlayerSync] 自動綁定 Left Hand: " + leftHand.name);
            }

            if (rightHand == null)
            {
                rightHand = FindHandObject("Right");
                if (rightHand != null) Debug.Log("[PlayerSync] 自動綁定 Right Hand: " + rightHand.name);
            }
        }
    }

    // 輔助方法：尋找含有特定關鍵字的控制器物件
    Transform FindHandObject(string sideKeyword)
    {
        // 策略 A: 直接搜尋常見名稱 (最準確)
        GameObject obj = GameObject.Find($"{sideKeyword} Controller"); // Unity 預設
        if (obj == null) obj = GameObject.Find($"{sideKeyword}Hand Controller"); // 常見變體
        if (obj == null) obj = GameObject.Find($"{sideKeyword}Hand"); // 簡寫

        if (obj != null) return obj.transform;

        // 策略 B: 如果上面都找不到，搜尋所有物件 (較慢，但能抓到怪異命名)
        // 只有在真的找不到時才執行這個
        var allControllers = GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var script in allControllers)
        {
            // 檢查是否是 XR 控制器相關腳本 (ActionBasedController 或其他)
            if (script.GetType().Name.Contains("Controller"))
            {
                // 檢查物件名稱是否包含 "Left" 或 "Right" (忽略大小寫)
                if (script.gameObject.name.IndexOf(sideKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return script.transform;
                }
            }
        }

        return null;
    }

    void SendPlayerState()
    {
        if (head != null && leftHand != null && rightHand != null)
        {
            PoseData[] allPoses = new PoseData[3];
            allPoses[IDX_HEAD] = CreatePose(head);
            allPoses[IDX_L_HAND] = CreatePose(leftHand);
            allPoses[IDX_R_HAND] = CreatePose(rightHand);
            NetworkManager.Instance.Send<PoseData[]>("Poses", allPoses);
        }
        else if (head != null)
        {
            PoseData headData = CreatePose(head);
            NetworkManager.Instance.Send<PoseData>("HeadPose", headData);
        }
    }

    PoseData CreatePose(Transform target)
    {
        return new PoseData
        {
            pos = new float[] { target.position.x, target.position.y, target.position.z },
            rot = new float[] { target.rotation.x, target.rotation.y, target.rotation.z, target.rotation.w }
        };
    }
}