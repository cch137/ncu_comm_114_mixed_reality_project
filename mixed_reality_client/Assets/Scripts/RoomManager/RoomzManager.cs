using UnityEngine;
using UnityEngine.InputSystem;

public class RoomManager : MonoBehaviour
{
    [Header("房間設定")]
    public string roomID = "101"; // 這就是你要的 public 變數，可以在 Inspector 改

    private void Start()
    {
        // 訂閱 NetworkManager 的事件
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnJoinRoomOK += HandleJoinSuccess;
        }
    }

    private void Update()
    {
        // 監聽 Q 鍵
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log($"按下 Q 鍵，嘗試加入房間: {roomID}");
            JoinRoom(roomID);
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Debug.Log($"按下 E 鍵，嘗試離開房間: {roomID}");
            LeaveRoom(roomID);
        }
    }

    // 抽出一個共用的 Join 方法，給按鍵或按鈕都可以用
    public void JoinRoom(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        RoomData data = new RoomData { id = id };

        // ★注意：這裡要用泛型 Send<RoomData>
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<RoomData>("JoinRoom", data);
        }
    }

    // 抽出一個共用的 Leave 方法
    public void LeaveRoom(string id)
    {
        RoomData data = new RoomData { id = id };

        if (NetworkManager.Instance != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            NetworkManager.Instance.Send<RoomData>("LeaveRoom", data);
        }
    }

    // --- 給 UI 按鈕綁定用的方法 (保留原本功能) ---
    public void OnJoinButtonPress(string idFromInput)
    {
        JoinRoom(idFromInput);
    }

    public void OnLeaveButtonPress(string idFromInput)
    {
        LeaveRoom(idFromInput);
    }
    // ------------------------------------------

    private void HandleJoinSuccess(string roomId)
    {
        Debug.Log($"<color=green>成功加入房間: {roomId}，切換 UI...</color>");
        // 這裡寫隱藏大廳 UI、顯示遊戲 UI 的邏輯
        // 例如: UIManager.Instance.ShowGamePanel();
    }

    private void OnDestroy()
    {
        // 記得取消訂閱防止 Memory Leak
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnJoinRoomOK -= HandleJoinSuccess;
        }
    }
}