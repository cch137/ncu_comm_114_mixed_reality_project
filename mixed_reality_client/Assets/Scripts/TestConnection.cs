using UnityEngine;
using NativeWebSocket;
using System.Text;
using UnityEngine.InputSystem;

public class TestConnection : MonoBehaviour
{
    private WebSocket websocket;

    [Header("連線設定")]
    public string serverUrl = "ws://localhost:8080";

    async void Start()
    {
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => Debug.Log("連線已開啟！");
        websocket.OnError += (e) => Debug.LogError("錯誤: " + e);
        websocket.OnClose += (e) => Debug.Log("連線已關閉！");

        // ==========================================
        // 【整合關鍵】收到訊息後的處理邏輯
        // ==========================================
        websocket.OnMessage += (bytes) =>
        {
            // 1. 解碼 JSON
            string jsonStr = Encoding.UTF8.GetString(bytes);
            Debug.Log($"收到 Server 訊息: {jsonStr}");

            try
            {
                // 2. 解析封包結構
                SimplePacket pack = JsonUtility.FromJson<SimplePacket>(jsonStr);

                // 3. 判斷類型：如果是 "ai_spawn"，就呼叫 Loader
                if (pack.type == "ai_spawn")
                {
                    string url = pack.content;

                    // 呼叫 MultiRuntimeLoader (確保它是 Main Thread 執行)
                    // NativeWebSocket 的 DispatchMessageQueue 會幫我們處理 Thread 問題
                    if (MultiRuntimeLoader.Instance != null)
                    {
                        MultiRuntimeLoader.Instance.LoadFromScript(url);
                    }
                    else
                    {
                        Debug.LogError("找不到 RuntimeLoader！請確認場景中有掛載該腳本。");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("JSON 解析失敗，可能格式不符: " + e.Message);
            }
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null) websocket.DispatchMessageQueue();
#endif

        // 保留空白鍵測試功能
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SendJson("log", "Client is alive!");
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null) await websocket.Close();
    }

    void SendJson(string type, string content)
    {
        if (websocket.State == WebSocketState.Open)
        {
            SimplePacket packet = new SimplePacket();
            packet.type = type;
            packet.content = content;
            string json = JsonUtility.ToJson(packet);
            websocket.SendText(json);
        }
    }
}

// 定義封包結構 (兩個腳本要講同一種語言)
[System.Serializable]
public class SimplePacket
{
    public string type;    // 用來區分功能，例如 "ai_spawn", "chat", "move"
    public string content; // 具體內容，例如網址
}