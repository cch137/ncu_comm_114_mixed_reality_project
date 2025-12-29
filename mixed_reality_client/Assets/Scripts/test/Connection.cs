//using UnityEngine;
//using NativeWebSocket;
//using System.Text;
//using UnityEngine.InputSystem;
//using Newtonsoft.Json;       // 引入 Newtonsoft
//using Newtonsoft.Json.Linq;  // 引入 JObject

//public class Connection : MonoBehaviour
//{
//    private WebSocket websocket;

//    [Header("連線設定")]
//    public string serverUrl = "ws://localhost:8080";
//    public bool autoReconnect = true; // 是否開啟自動重連

//    private bool isQuitting = false; // 避免關閉程式時觸發重連

//    void Start()
//    {
//        ConnectToServer();
//    }

//    async void ConnectToServer()
//    {
//        // 如果已經有連線，先清除舊的
//        if (websocket != null)
//        {
//            websocket = null;
//        }

//        websocket = new WebSocket(serverUrl);

//        websocket.OnOpen += () => Debug.Log("<color=green>連線已開啟！</color>");

//        // 錯誤處理
//        websocket.OnError += (e) =>
//        {
//            Debug.LogError("WebSocket 錯誤: " + e);
//            // 通常 Error 後面會接 Close，所以重連邏輯寫在 OnClose 比較保險
//        };

//        // ==========================================
//        // 【斷線重連邏輯】
//        // ==========================================
//        websocket.OnClose += (e) =>
//        {
//            Debug.LogWarning("連線已關閉！");

//            if (autoReconnect && !isQuitting)
//            {
//                Debug.Log("1 秒後嘗試重新連線...");
//                // 使用 Invoke 延遲 1 秒後執行連線函式
//                Invoke(nameof(ConnectToServer), 1.0f);
//            }
//        };

//        // ==========================================
//        // 【Newtonsoft 整合】收到訊息後的處理邏輯
//        // ==========================================
//        websocket.OnMessage += (bytes) =>
//        {
//            string jsonStr = Encoding.UTF8.GetString(bytes);
//            Debug.Log($"收到 Server 訊息: {jsonStr}");

//            try
//            {
//                // 1. 使用 JObject 解析 (Newtonsoft)
//                JObject pack = JObject.Parse(jsonStr);

//                // 取得 type (使用 ?. 避免 null 報錯)
//                string msgType = pack["type"]?.ToString();

//                switch (msgType)
//                {
//                    case "LoadGLTF":
//                        // 直接透過路徑存取巢狀資料
//                        string id = pack["data"]?["id"]?.ToString();
//                        string name = pack["data"]?["name"]?.ToString();
//                        string url = pack["data"]?["url"]?.ToString();

//                        Debug.Log($"伺服器要求載入 GLTF, URL: {url}");

//                        if (!string.IsNullOrEmpty(url))
//                        {
//                            if (MultiRuntimeLoader.Instance != null)
//                            {
//                                MultiRuntimeLoader.Instance.LoadFromScript(id,name,url);
//                            }
//                            else
//                            {
//                                Debug.LogError("找不到 MultiRuntimeLoader！請確認場景中有掛載該腳本。");
//                            }
//                        }
//                        break;

//                    case "Heartbeat":
//                        Debug.Log("收到心跳包: " + pack["data"]);
//                        break;

//                    default:
//                        Debug.LogWarning("未知的封包類型: " + msgType);
//                        break;
//                }
//            }
//            catch (System.Exception e)
//            {
//                Debug.LogError($"JSON 解析失敗: {e.Message} \n原始資料: {jsonStr}");
//            }
//        };

//        // 開始連線
//        await websocket.Connect();
//    }

//    void Update()
//    {
//#if !UNITY_WEBGL || UNITY_EDITOR
//        if (websocket != null) websocket.DispatchMessageQueue();
//#endif

//        // 空白鍵測試發送
//        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
//        {
//            // 測試傳送一個複雜一點的物件
//            var testData = new { message = "Client is alive!", timestamp = Time.time };
//            SendJson("log", testData);
//        }
//    }

//    private async void OnApplicationQuit()
//    {
//        isQuitting = true; // 標記為正在關閉，阻止重連
//        CancelInvoke(nameof(ConnectToServer)); // 取消尚未執行的重連排程

//        if (websocket != null)
//        {
//            await websocket.Close();
//        }
//    }

//    void SendJson(string type, object content)
//    {
//        if (websocket != null && websocket.State == WebSocketState.Open)
//        {
//            // 建構封包
//            SimplePacket packet = new SimplePacket();
//            packet.type = type;
//            packet.data = content;

//            // 使用 Newtonsoft 序列化 (因為 content 是 object 類型，JsonUtility 處理不了)
//            string json = JsonConvert.SerializeObject(packet);

//            websocket.SendText(json);
//        }
//    }
//}

//// 定義封包結構 (只用於發送，接收時我們已經改用 JObject 動態解析了)
//[System.Serializable]
//public class SimplePacket
//{
//    public string type;
//    public object data; // 使用 object 讓 Newtonsoft 可以塞入任何結構
//}