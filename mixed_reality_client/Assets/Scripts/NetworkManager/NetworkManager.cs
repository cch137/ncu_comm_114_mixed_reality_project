using UnityEngine;
using NativeWebSocket; // 引用插件
using System;
using System.Text;
// 移除 System.Net.WebSockets 避免衝突，或者使用下方的 alias
using WebSocket = NativeWebSocket.WebSocket;
using WebSocketState = NativeWebSocket.WebSocketState;

public class NetworkManager : MonoBehaviour
{
    [Header("連線設定")]
    public string serverUrl = "wss://40001.cch137.com/mr-realtime";
    public bool autoReconnect = true;
    public static NetworkManager Instance;

    private bool isQuitting = false;
    private WebSocket websocket;

    // 定義事件
    public event Action<GLTFData> OnLoadGLTF;
    public event Action<string> OnJoinRoomOK;
    public event Action<string> OnError;
    public event Action<AudioData> OnAudioReceived;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ConnectToServer();
    }

    // ★★★ 關鍵修正：必須在 Update 裡驅動訊息佇列 ★★★
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }

    // 當程式關閉時，優雅斷線
    private async void OnApplicationQuit()
    {
        isQuitting = true;
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    async void ConnectToServer()
    {
        if (websocket != null) websocket = null;

        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => Debug.Log("<color=green>連線已開啟！</color>");

        websocket.OnError += (e) => Debug.LogError("WebSocket 錯誤: " + e);

        websocket.OnClose += (e) =>
        {
            Debug.LogWarning("連線已關閉！");
            if (autoReconnect && !isQuitting)
            {
                Debug.Log("1 秒後嘗試重新連線...");
                Invoke(nameof(ConnectToServer), 1.0f);
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            string payload = Encoding.UTF8.GetString(bytes);
            // Debug.Log("收到server訊息:" + payload); // 怕洗頻可以註解掉
            HandleMessage(payload);
        };

        await websocket.Connect();
    }

    void HandleMessage(string jsonString)
    {
        // 解析 Type
        BaseMessage baseMsg = JsonUtility.FromJson<BaseMessage>(jsonString);

        if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) return;

        switch (baseMsg.type)
        {
            case "JoinRoomOK":
                var roomMsg = JsonUtility.FromJson<MessageWrapper<RoomData>>(jsonString);
                Debug.Log($"加入房間成功: {roomMsg.data.id}");
                OnJoinRoomOK?.Invoke(roomMsg.data.id);
                break;

            case "JoinRoomError":
                var errorMsg = JsonUtility.FromJson<MessageWrapper<RoomErrorData>>(jsonString);
                Debug.LogError($"加入失敗: {errorMsg.data.reason}");
                break;

            case "LoadGLTF":
                var gltfMsg = JsonUtility.FromJson<MessageWrapper<GLTFData>>(jsonString);
                Debug.Log($"收到生成指令: {gltfMsg.data.name}");
                OnLoadGLTF?.Invoke(gltfMsg.data);
                break;

            case "Ping":
                // 這裡傳送空字串當 payload，泛型會自動包裝成 { type: "Pong", payload: "" }
                Send<string>("Pong", "");
                break;

            case "Pong":
                // 這裡傳送空字串當 payload，泛型會自動包裝成 { type: "Pong", payload: "" }
                Send<string>("Ping", "");
                break;

            case "Error":
                // 假設你有定義 ErrorMsg 類別
                var errMsg = JsonUtility.FromJson<MessageWrapper<ErrorMsg>>(jsonString);
                Debug.LogError($"Server 回報錯誤: {errMsg.data.errorMessage}");
                break;

            case "Audio":
                var audioMsg = JsonUtility.FromJson<MessageWrapper<AudioData>>(jsonString);
                // 這裡通常資料量很大，建議不要 Debug.Log，不然 Unity 會卡死
                OnAudioReceived?.Invoke(audioMsg.data);
                break;
        }
    }

    public void Send<T>(string eventName, T payloadData)
    {
        MessageWrapper<T> wrapper = new MessageWrapper<T>();
        wrapper.type = eventName;
        wrapper.data = payloadData;

        string finalJson = JsonUtility.ToJson(wrapper);

        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            websocket.SendText(finalJson);
        }
        // Debug.Log($"[Send] {eventName}: {finalJson}");
    }
}