using UnityEngine;
using System.Collections;
using GLTFast;
using System.Threading.Tasks;
using UnityEngine.InputSystem; // 引用新版輸入系統

public class MultiRuntimeLoader : MonoBehaviour
{
    // 單例模式：讓別的腳本可以找到我
    public static MultiRuntimeLoader Instance;

    [Header("測試設定")]
    [Tooltip("手動測試用的網址 (按 D 生成)")]
    public string publicModelUrl = "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF-Binary/Duck.glb";

    [Tooltip("生成半徑")]
    public float spawnRadius = 2.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // 本地測試功能 (保留給你自己除錯用)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.wasPressedThisFrame) SpawnOneAtRandomPosition();
        }
    }

    // ==========================================
    // 【整合關鍵】給 WebSocket 呼叫的入口
    // ==========================================
    public void LoadFromScript(string newUrl)
    {
        Debug.Log($"[系統指令] WebSocket 請求下載: {newUrl}");

        // 更新 Inspector 方便觀察
        publicModelUrl = newUrl;

        // 執行生成
        SpawnOneAtRandomPosition();
    }

    // ==========================================
    // 生成邏輯
    // ==========================================
    public void SpawnOneAtRandomPosition()
    {
        Vector3 randomPos = new Vector3(
            Random.Range(-spawnRadius, spawnRadius),
            0,
            Random.Range(-spawnRadius, spawnRadius)
        );
        LoadModel(publicModelUrl, randomPos);
    }

    private async void LoadModel(string url, Vector3 position)
    {
        if (string.IsNullOrEmpty(url)) return;

        var gltf = new GltfImport();
        bool success = await gltf.Load(url);

        if (success)
        {
            GameObject modelObj = new GameObject($"AI_Model_{Time.frameCount}");
            modelObj.transform.position = position;
            await gltf.InstantiateMainSceneAsync(modelObj.transform);

            // 自動加 Collider
            var collider = modelObj.AddComponent<BoxCollider>();
            Renderer[] renderers = modelObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                collider.center = bounds.center - modelObj.transform.position;
                collider.size = bounds.size;
            }
        }
        else
        {
            Debug.LogError($"[RuntimeLoader] 下載失敗: {url}");
        }
    }
}