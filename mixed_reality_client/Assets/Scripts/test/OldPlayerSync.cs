using UnityEngine;

public class OldPlayerSync : MonoBehaviour
{
    [Header("追蹤物件 (請拖曳 VR Camera 和 Controllers)")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("同步設定")]
    [Tooltip("發送頻率 (秒)，建議 0.05 ~ 0.1")]
    public float syncRate = 0.1f;
    private float timer;

    // 定義 API 規定的陣列索引 (Internal Constants)
    private const int IDX_HEAD = 0;
    private const int IDX_L_HAND = 1;
    private const int IDX_R_HAND = 2;

    void Update()
    {
        // 只有在連線建立後才開始同步
        if (NetworkManager.Instance == null) return;

        if (RoomManager.Instance == null || !RoomManager.Instance.IsInRoom) return;

        timer += Time.deltaTime;
        if (timer >= syncRate)
        {
            SendPlayerState();
            timer = 0;
        }
    }

    void SendPlayerState()
    {
        // 情況 A: 頭與雙手都在 -> 使用新版 "Poses" 陣列 (綠色區域規格)
        if (head != null && leftHand != null && rightHand != null)
        {
            // 建立長度為 3 的陣列
            PoseData[] allPoses = new PoseData[3];

            // 依序填入：0:頭, 1:左手, 2:右手
            allPoses[IDX_HEAD] = CreatePose(head);
            allPoses[IDX_L_HAND] = CreatePose(leftHand);
            allPoses[IDX_R_HAND] = CreatePose(rightHand);

            // 發送事件
            NetworkManager.Instance.Send<PoseData[]>("Poses", allPoses);
        }
        // 情況 B: 只有頭 (例如 3DoF 設備) -> 降級使用 "HeadPose"
        else if (head != null)
        {
            PoseData headData = CreatePose(head);
            NetworkManager.Instance.Send<PoseData>("HeadPose", headData);
        }
    }

    // 輔助方法：將 Unity Transform 轉換為 Server 需要的 PoseData
    PoseData CreatePose(Transform target)
    {
        return new PoseData
        {
            // Unity Vector3 -> float array
            pos = new float[] { target.position.x, target.position.y, target.position.z },
            // Unity Quaternion -> float array
            rot = new float[] { target.rotation.x, target.rotation.y, target.rotation.z, target.rotation.w }
        };
    }
}