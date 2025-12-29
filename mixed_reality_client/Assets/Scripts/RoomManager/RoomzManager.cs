using UnityEngine;
using UnityEngine.InputSystem;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("房間設定")]
    public string desireRoomID = "101";
    public string CurrentRoomId { get; private set; }

    // 判斷是否在房間內的屬性
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
            // 訂閱斷線事件
            NetworkManager.Instance.OnDisconnected += HandleDisconnected;
        }
    }

    private void Update()
    {
        // 按鍵 O 加入
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
            Debug.Log($"[按鍵] 按下 O 鍵，嘗試加入房間: {desireRoomID}");
            JoinRoom(desireRoomID);
        }

        // 按鍵 P 離開
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            Debug.Log($"[按鍵] 按下 P 鍵，嘗試離開房間: {desireRoomID}");
            LeaveRoom(desireRoomID);
        }
    }

    // --- 功能邏輯 ---

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

    // --- UI 呼叫端 ---

    public void OnJoinButtonPress(string idFromInput)
    {
        JoinRoom(idFromInput);
    }

    public void OnLeaveButtonPress(string idFromInput)
    {
        LeaveRoom(idFromInput);
    }

    // --- 回調處理 ---

    private void HandleJoinSuccess(string roomId)
    {
        // [安全機制] 加入新房間前，先確保舊物件都清乾淨了，避免殘留
        if (EntityManager.Instance != null)
        {
            EntityManager.Instance.ClearAll();
        }

        Debug.Log($"<color=green>成功加入房間: {roomId}</color>");
        CurrentRoomId = roomId;

        // TODO: 這裡可以加入切換 UI 的邏輯 (隱藏 Lobby, 顯示 Game UI)
    }

    private void HandleLeaveSuccess(string roomId)
    {
        Debug.Log($"<color=yellow>已離開房間: {roomId}</color>");
        ClearRoomState(); // 呼叫統一的清理邏輯
    }

    // 處理意外斷線
    private void HandleDisconnected()
    {
        // 如果原本在房間內，才需要做清理動作
        if (IsInRoom)
        {
            Debug.LogWarning($"[RoomManager] 偵測到斷線！強制執行房間清理。");
            ClearRoomState();
        }
    }

    // --- 核心清理邏輯 ---
    // 無論是「主動離開」還是「被動斷線」，都走這條路
    private void ClearRoomState()
    {
        CurrentRoomId = ""; // 1. 清空 ID，標記為不在房間

        // 2. 清空場景上的網路物件
        if (EntityManager.Instance != null)
        {
            // 這裡呼叫 EntityManager 的 ClearAll 方法 (記得去 EntityManager 把該方法補上並公開)
            EntityManager.Instance.ClearAll();
            Debug.Log("[RoomManager] 已請求 EntityManager 清空所有物件...");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnJoinRoomOK -= HandleJoinSuccess;
            NetworkManager.Instance.OnLeaveRoomOK -= HandleLeaveSuccess;
            NetworkManager.Instance.OnDisconnected -= HandleDisconnected;
        }
    }
}