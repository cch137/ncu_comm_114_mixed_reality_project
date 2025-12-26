using UnityEngine;
using System.Collections;
using GLTFast;
using System.Threading.Tasks;
using UnityEngine.InputSystem; // 【新增】引用新版輸入系統

public class RuntimeLoader : MonoBehaviour
{
    public static RuntimeLoader Instance;

    [Header("設定")]
    [Tooltip("請在這裡填入你的 GLB/GLTF 模型網址")]
    public string publicModelUrl = "https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Duck/glTF-Binary/Duck.glb";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // 【修改】改用 New Input System 偵測 S 鍵
        // 舊版寫法: if (Input.GetKeyDown(KeyCode.S))
        if (Keyboard.current != null && Keyboard.current.sKey.wasPressedThisFrame)
        {
            StartGameProcess();
        }
    }

    public void StartGameProcess()
    {
        Debug.Log("遊戲開始");
        StartCoroutine(WaitAndSpawnRoutine());
    }

    IEnumerator WaitAndSpawnRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        Debug.Log("開始生成");
        LoadModel(publicModelUrl, Vector3.zero);
    }

    private async void LoadModel(string url, Vector3 position)
    {
        if (string.IsNullOrEmpty(url)) return;
        Debug.Log($"[RuntimeLoader] 正在下載: {url}");

        var gltf = new GltfImport();
        bool success = await gltf.Load(url);

        if (success)
        {
            GameObject modelObj = new GameObject("Downloaded_Model");
            modelObj.transform.position = position;
            await gltf.InstantiateMainSceneAsync(modelObj.transform);
            Debug.Log("[RuntimeLoader] 模型生成完畢！");
        }
        else
        {
            Debug.LogError("[RuntimeLoader] 下載失敗");
        }
    }
}