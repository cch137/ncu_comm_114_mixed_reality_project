using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using WebSocket = NativeWebSocket.WebSocket;
using WebSocketState = NativeWebSocket.WebSocketState;

public class NetworkManager : MonoBehaviour
{
    [Header("連線設定")]
    public string serverUrl = "wss://40001.cch137.com/mr-realtime";
    public bool autoReconnect = true;
    public static NetworkManager Instance;
    public event Action OnDisconnected;

    private bool isQuitting = false;
    private WebSocket websocket;

    // --- 事件定義 ---
    public event Action<string> OnJoinRoomOK;
    public event Action<string> OnLeaveRoomOK;
    public event Action<AudioData> OnAudioReceived;

    // 實體相關事件
    public event Action<CreateProgObjData> OnCreateProgObj;
    public event Action<EntityData> OnCreateGeomObj;
    public event Action<EntityData> OnCreateAnchor;
    public event Action<EntityData> OnUpdateEntity;
    public event Action<DeleteEntityData> OnDelEntity;
    public event Action OnFlushAudio;
    public event Action<string> OnTranscriptReceived;

    // 錯誤處理
    public event Action<string> OnError;

    [Header("Components")]
    [SerializeField] private NetworkLogger _logger;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    private void Start() => ConnectToServer();

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null) websocket.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        isQuitting = true;
        if (websocket != null) await websocket.Close();
    }

    async void ConnectToServer()
    {
        if (websocket != null) websocket = null;
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("<color=green>連線已開啟！</color>");
            if (_logger != null)
            {
                _logger.ResetCounters();
            }
        };
        websocket.OnError += (e) => Debug.LogError("WebSocket 錯誤: " + e);
        websocket.OnClose += (e) => {
            Debug.LogWarning("連線已關閉！");
            if (autoReconnect && !isQuitting)
            {
                OnDisconnected?.Invoke();
                if (autoReconnect && !isQuitting)
                {
                    Debug.Log("1 秒後嘗試重新連線...");
                    Invoke(nameof(ConnectToServer), 1.0f);
                }
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            string payload = Encoding.UTF8.GetString(bytes);
            // Debug.Log("收到原始 Payload: " + payload); // <--- Data 部分保持註解
            HandleMessage(payload);
        };

        await websocket.Connect();
    }

    void HandleMessage(string jsonString)
    {
        BaseMessage baseMsg = JsonUtility.FromJson<BaseMessage>(jsonString);
        if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) return;

        // ★★★ 修改處：只輸出 Type，詳細 Data (jsonString) 註解掉 ★★★
        _logger.LogReceive(baseMsg.type, jsonString);

        switch (baseMsg.type)
        {
            // --- 1. 實體建立與更新 ---
            case "CreateEntityProgObj":
                var progMsg = JsonUtility.FromJson<MessageWrapper<CreateProgObjData>>(jsonString);
                OnCreateProgObj?.Invoke(progMsg.data);
                break;

            case "CreateEntityGeomObj":
                var geomMsg = JsonUtility.FromJson<MessageWrapper<EntityData>>(jsonString);
                OnCreateGeomObj?.Invoke(geomMsg.data);
                break;

            case "CreateEntityAnchor":
                var anchorMsg = JsonUtility.FromJson<MessageWrapper<EntityData>>(jsonString);
                OnCreateAnchor?.Invoke(anchorMsg.data);
                break;

            case "UpdateEntity":
                var updateMsg = JsonUtility.FromJson<MessageWrapper<EntityData>>(jsonString);
                OnUpdateEntity?.Invoke(updateMsg.data);
                break;

            case "DelEntity":
                var delMsg = JsonUtility.FromJson<MessageWrapper<DeleteEntityData>>(jsonString);
                OnDelEntity?.Invoke(delMsg.data);
                break;

            // --- 2. 房間與系統 ---
            case "JoinRoomOK":
                var joinOk = JsonUtility.FromJson<MessageWrapper<RoomData>>(jsonString);
                OnJoinRoomOK?.Invoke(joinOk.data.id);
                break;

            case "JoinRoomError":
                var joinErr = JsonUtility.FromJson<MessageWrapper<RoomErrorData>>(jsonString);
                Debug.LogError($"加入房間失敗: {joinErr.data.reason}");
                // 建議：這裡可以觸發一個事件讓 UI 顯示錯誤視窗
                break;

            case "LeaveRoomError":
                var leaveErr = JsonUtility.FromJson<MessageWrapper<RoomErrorData>>(jsonString);
                Debug.LogError($"離開房間失敗: {leaveErr.data.reason}");
                break;

            case "LeaveRoomOK":
                var leaveOk = JsonUtility.FromJson<MessageWrapper<RoomData>>(jsonString);
                OnLeaveRoomOK?.Invoke(leaveOk.data.id);
                break;

            case "Ping":
                Send<string>("Pong", ""); // 收到 Ping 回應 Pong
                break;

            case "Audio":
                var audio = JsonUtility.FromJson<MessageWrapper<AudioData>>(jsonString);
                OnAudioReceived?.Invoke(audio.data);
                break;

            case "FlushAudio":
                // 收到打斷訊號，觸發事件
                Debug.Log("<color=magenta>[Network] 收到 FlushAudio，要求清空音訊緩衝</color>");
                OnFlushAudio?.Invoke();
                break;

            case "Transcript":
                var transMsg = JsonUtility.FromJson<MessageWrapper<TranscriptMsg>>(jsonString);

                if (transMsg != null && transMsg.data != null)
                {
                    string textContent = transMsg.data.message;

                    Debug.Log($"<color=cyan>[Transcript] AI 說: {textContent}</color>");
                    OnTranscriptReceived?.Invoke(textContent);
                }
                break;

            case "Error":
                var err = JsonUtility.FromJson<MessageWrapper<ErrorMsg>>(jsonString);
                Debug.LogError($"Server Error: {err.data.message}");
                OnError?.Invoke(err.data.message);
                break;

            default:
                Debug.LogWarning($"未定義的類型: {baseMsg.type}");
                break;
        }
    }

    public void Send<T>(string eventName, T payloadData)
    {
        MessageWrapper<T> wrapper = new MessageWrapper<T>();
        wrapper.type = eventName;
        wrapper.data = payloadData;

        // ★★★ 修改處：只輸出 Type，詳細 Data (jsonString) 註解掉 ★★★
        _logger.LogSend(eventName, payloadData);

        string finalJson = JsonUtility.ToJson(wrapper);

        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            websocket.SendText(finalJson);
        }
        // Debug.Log($"[Send] Type: {eventName}"); // 發送時也只留 Type，Data 註解掉
    }
}