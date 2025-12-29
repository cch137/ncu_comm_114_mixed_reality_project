using UnityEngine;

public class ObjGrabbable : MonoBehaviour
{
    [Header("物件 ID (由 Spawn 生成時自動填入)")]
    public string entityId;

    [Header("同步設定")]
    public float updateRate = 0.1f;
    public float smoothSpeed = 10f; // 補間動畫速度，數值越大越快

    private bool isGrabbed = false;
    private float timer;

    // 接收到的目標位置 (用於平滑移動)
    private Vector3 targetPos;
    private Quaternion targetRot;
    private bool hasReceivedInitialState = false;

    private Rigidbody rb;

    // 用來記錄這物件是否屬於「我」
    public bool IsOwnedByMe => isGrabbed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // 初始化目標位置為當前位置，避免物件亂飛
        targetPos = transform.position;
        targetRot = transform.rotation;
    }

    private void Start()
    {
        // 訂閱：監聽別人的移動事件
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnUpdateEntity += HandleRemoteUpdate;
        }
    }

    private void OnDestroy()
    {
        // 反註冊：避免 Memory Leak
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnUpdateEntity -= HandleRemoteUpdate;
        }
    }

    // --- 外部呼叫介面 (給 VR Grab Event 使用) ---

    // 1. 當玩家「抓起」物件
    public void OnGrabStart()
    {
        if (string.IsNullOrEmpty(entityId)) return;

        isGrabbed = true;

        // 抓取時，恢復物理控制 (視你的 VR SDK 需求而定，通常抓取時要是 Kinematic 或由 Hand 控制)
        if (rb != null) rb.isKinematic = true;

        // 發送搶奪擁有權事件
        EntityControlData data = new EntityControlData { id = entityId };
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<EntityControlData>("ClaimEntity", data);
        }

        Debug.Log($"[互動] Claim 物件: {entityId}");
    }

    // 2. 當玩家「放開」物件
    public void OnGrabEnd()
    {
        if (string.IsNullOrEmpty(entityId)) return;

        isGrabbed = false;

        // 放開後，如果有 Rigidbody，可能需要恢復重力 (視需求)
        if (rb != null) rb.isKinematic = false;

        // 發送釋放擁有權事件
        EntityControlData data = new EntityControlData { id = entityId };
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<EntityControlData>("ReleaseEntity", data);
        }

        Debug.Log($"[互動] Release 物件: {entityId}");
    }

    // --- Update 迴圈 ---

    private void Update()
    {
        // 情況 A: 我抓著物件 -> 發送座標給 Server
        if (isGrabbed)
        {
            // 更新本機目標，防止放開瞬間回彈
            targetPos = transform.position;
            targetRot = transform.rotation;

            if (NetworkManager.Instance != null)
            {
                timer += Time.deltaTime;
                if (timer >= updateRate)
                {
                    SendTransformUpdate();
                    timer = 0;
                }
            }
        }
        // 情況 B: 別人抓著 (或沒人抓) -> 接收 Server 座標並平滑移動
        else
        {
            // 只有當接收過第一次資料後才開始移動，避免生成時歸零
            if (hasReceivedInitialState)
            {
                // 使用 Lerp (線性插值) 讓移動變滑順
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
            }
        }
    }

    // --- 發送與接收邏輯 ---

    private void SendTransformUpdate()
    {
        EntityData data = new EntityData
        {
            id = entityId,
            pose = new PoseData
            {
                pos = new float[] { transform.position.x, transform.position.y, transform.position.z },
                rot = new float[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w }
            }
        };

        NetworkManager.Instance.Send<EntityData>("UpdateEntity", data);
    }

    // ★★★ 關鍵補充：處理從 Server 收到的更新 ★★★
    private void HandleRemoteUpdate(EntityData data)
    {
        // 1. 檢查 ID 是否吻合
        if (data.id != entityId) return;

        // 2. 如果是我自己抓著的，就忽略 Server 的舊資料 (Client Side Prediction)
        if (isGrabbed) return;

        // 3. 更新目標位置 (在 Update 裡執行平滑移動)
        if (data.pose != null)
        {
            if (data.pose.pos != null && data.pose.pos.Length == 3)
            {
                targetPos = new Vector3(data.pose.pos[0], data.pose.pos[1], data.pose.pos[2]);
            }

            if (data.pose.rot != null && data.pose.rot.Length == 4)
            {
                targetRot = new Quaternion(data.pose.rot[0], data.pose.rot[1], data.pose.rot[2], data.pose.rot[3]);
            }

            hasReceivedInitialState = true;

            // 4. (選用) 強制設為 Kinematic，避免物理干擾同步
            if (rb != null && !rb.isKinematic) rb.isKinematic = true;
        }
    }
}