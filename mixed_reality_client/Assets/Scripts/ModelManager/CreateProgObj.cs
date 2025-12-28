using UnityEngine;
using GLTFast;
using System.Threading.Tasks;

public class CreateProgObj : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnCreateProgObj += HandleCreateProgObj;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnCreateProgObj -= HandleCreateProgObj;
        }
    }

    private async void HandleCreateProgObj(CreateProgObjData data)
    {
        Debug.Log($"[CreateProgObj] 開始下載: {data.gltf.url}");

        // 1. 建立空物件作為容器
        GameObject container = new GameObject($"ProgObj_{data.gltf.name}");

        // 設定初始位置
        EntityManager.Instance.ApplyPose(container.transform, data.pose);

        // 3. 註冊到管理器
        EntityManager.Instance.RegisterEntity(data.id, container);

        // ★★★ 新增：自動掛載 NetworkGrabbable ★★★
        var grabbable = container.AddComponent<ObjGrabbable>();
        grabbable.entityId = data.id; // 重要：必須填入 ID，腳本才知道要 Claim 誰
        // grabbable.updateRate = 0.1f; // 如果想客製化頻率可以在這改

        // 4. 開始下載 GLTF
        await LoadGLTF(data.gltf.url, container.transform);
    }

    private async Task LoadGLTF(string url, Transform parent)
    {
        if (string.IsNullOrEmpty(url)) return;

        var gltf = new GltfImport();
        bool success = await gltf.Load(url);

        if (success)
        {
            await gltf.InstantiateMainSceneAsync(parent);

            // 自動加 Collider (沿用您原本的邏輯)
            // 注意：這裡的 parent 是容器，模型是它的子物件
            Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && parent.GetComponent<Collider>() == null)
            {
                var collider = parent.gameObject.AddComponent<BoxCollider>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

                // 轉換 bounds center 到 local space
                collider.center = parent.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
            }
        }
        else
        {
            Debug.LogError($"[CreateProgObj] 下載失敗: {url}");
        }
    }
}