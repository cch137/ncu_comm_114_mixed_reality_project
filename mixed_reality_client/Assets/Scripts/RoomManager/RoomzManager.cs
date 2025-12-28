using UnityEngine;
using UnityEngine.InputSystem;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("房間設定")]
    public string desireRoomID = "101";
    public string CurrentRoomId { get; private set; }

    // ★修正 1：補上原本漏掉的分號 ";"
    public bool IsInRoom => !string.IsNullOrEmpty(CurrentRoomId);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnJoinRoomOK += HandleJoinSuccess;
            NetworkManager.Instance.OnLeaveRoomOK += HandleLeaveSuccess;

            // ★修改 2：訂閱斷線事件
            NetworkManager.Instance.OnDisconnected += HandleDisconnected;
        }
    }

    private void Update()
    {
        // 監聽 Q 鍵
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log($"[鍵盤] 按下 Q 鍵，嘗試加入房間: {desireRoomID}");
            JoinRoom(desireRoomID);
        }

        // 監聽 E 鍵
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log($"[鍵盤] 按下 E 鍵，嘗試離開房間: {desireRoomID}");
            LeaveRoom(desireRoomID);
        }
    }

    // --- 功能邏輯區 ---

    public void JoinRoom(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        RoomData data = new RoomData { id = id };

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<RoomData>("JoinRoom", data);
        }
    }

    public void LeaveRoom(string id)
    {
        RoomData data = new RoomData { id = id };

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<RoomData>("LeaveRoom", data);
        }
    }

    // --- UI 綁定區 ---

    public void OnJoinButtonPress(string idFromInput)
    {
        JoinRoom(idFromInput);
    }

    public void OnLeaveButtonPress(string idFromInput)
    {
        LeaveRoom(idFromInput);
    }

    // --- 回調處理區 ---

    private void HandleJoinSuccess(string roomId)
    {
        Debug.Log($"<color=green>成功加入房間: {roomId}</color>");
        CurrentRoomId = roomId;
        // TODO: 切換 UI 面板
    }

    private void HandleLeaveSuccess(string roomId)
    {
        Debug.Log($"<color=yellow>已離開房間: {roomId}</color>");
        ClearRoomState(); // 抽成共用方法
    }

    // ★修改 3：處理斷線邏輯
    private void HandleDisconnected()
    {
        // 只有原本在房間裡，才需要執行清理
        if (IsInRoom)
        {
            Debug.LogWarning($"[RoomManager] 網路斷線！強制執行房間清理。");
            ClearRoomState();
        }
    }

    // ★修改 4：把清理邏輯抽出來，讓「主動離開」和「被動斷線」都共用這段
    private void ClearRoomState()
    {
        CurrentRoomId = ""; // 1. 清空 ID

        // 2. 清空場景物件
        if (EntityManager.Instance != null)
        {
            // EntityManager.Instance.ClearAll(); // <--- 記得解開這行註解
            Debug.Log("呼叫 EntityManager 清空所有物件...");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnJoinRoomOK -= HandleJoinSuccess;
            NetworkManager.Instance.OnLeaveRoomOK -= HandleLeaveSuccess;

            // ★修改 5：記得取消訂閱斷線事件
            NetworkManager.Instance.OnDisconnected -= HandleDisconnected;
        }
    }
}