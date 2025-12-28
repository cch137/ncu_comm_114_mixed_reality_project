//using UnityEngine;
//using GLTFast;
//using System.Threading.Tasks;

//public class MultiRuntimeLoader : MonoBehaviour
//{
//    public static MultiRuntimeLoader Instance;

//    [Header("測試設定")]
//    [Tooltip("生成半徑")]
//    public float spawnRadius = 2.0f;

//    private void Start()
//    {
//        if (NetworkManager.Instance != null)
//        {
//            // 正確寫法：只寫方法名稱，不要加括號 ()
//            NetworkManager.Instance.OnLoadGLTF += OnGLTFReceived;
//        }
//    }

//    private void OnGLTFReceived(GLTFData payload)
//    {
//        Debug.Log($"[事件接收] 收到生成請求: {payload.url}");

//        // 這裡再去呼叫生成邏輯
//        SpawnOneAtRandomPosition(payload.id, payload.name, payload.url);
//        SendLoadSuccess(payload.id);
//    }

//    //// ==========================================
//    //// 給 WebSocket 呼叫的入口
//    //// ==========================================
//    //public void LoadFromScript(string newId, string newName, string newUrl)
//    //{
//    //    Debug.Log($"[系統指令] WebSocket 請求下載: {newUrl}");

//    //    // 直接把參數往下傳，不需要存到全域變數
//    //    SpawnOneAtRandomPosition(newId, newName, newUrl);
//    //}

//    public void SpawnOneAtRandomPosition(string newId, string newName, string newUrl)
//    {
//        Vector3 randomPos = new Vector3(
//            Random.Range(-spawnRadius, spawnRadius),
//            0,
//            Random.Range(-spawnRadius, spawnRadius)
//        );
//        LoadModel(newId, newName, newUrl, randomPos);
//    }

//    private async void LoadModel(string newId, string newName, string url, Vector3 position)
//    {
//        if (string.IsNullOrEmpty(url)) return;

//        var gltf = new GltfImport();
//        bool success = await gltf.Load(url);

//        if (success)
//        {
//            GameObject modelObj = new GameObject($"AI_Model_{newName}_{newId}");
//            modelObj.transform.position = position;
//            await gltf.InstantiateMainSceneAsync(modelObj.transform);

//            // 自動加 Collider
//            var collider = modelObj.AddComponent<BoxCollider>();
//            Renderer[] renderers = modelObj.GetComponentsInChildren<Renderer>();
//            if (renderers.Length > 0)
//            {
//                Bounds bounds = renderers[0].bounds;
//                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
//                collider.center = bounds.center - modelObj.transform.position;
//                collider.size = bounds.size;
//            }
//        }
//        else
//        {
//            Debug.LogError($"[MultiRuntimeLoader] 下載失敗: {url}");
//        }
//    }

//    public void SendLoadSuccess(string modelId)
//    {
//        // 1. 準備資料
//        GLTFResultData data = new GLTFResultData();
//        data.id = modelId;

//        // 2. 發送！
//        // 寫法：Send<資料類別>("事件名稱", 資料物件)
//        if (NetworkManager.Instance != null)
//        {
//            NetworkManager.Instance.Send<GLTFResultData>("LoadGLTFOK", data);
//            Debug.Log($"已通知 Server 模型載入完成: {data}");
//        }
//    }
//}