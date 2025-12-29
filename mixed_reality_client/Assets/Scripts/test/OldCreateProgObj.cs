using UnityEngine;
using GLTFast;
using System.Threading.Tasks;

public class OldCreateProgObj : MonoBehaviour
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
        Debug.Log($"[CreateProgObj] 準備下載: {data.gltf.url}");

        // 1. 先下載 (這時候還不建立物件，所以沒有 parent 會被刪除的問題)
        var gltf = new GltfImport();
        bool success = await gltf.Load(data.gltf.url);

        // 如果腳本本身在下載過程中被銷毀了，就停手
        if (this == null) return;

        if (success)
        {
            // 2. 下載成功了！現在才建立新的遊戲物件
            GameObject newObj = new GameObject($"ProgObj_{data.gltf.name}");

            // 3. 把模型生在剛剛建好的物件裡面
            await gltf.InstantiateMainSceneAsync(newObj.transform);

            // 安全檢查：生成過程中若腳本或物件被銷毀則停止
            if (this == null || newObj == null) return;

            // 4. 設定位置與註冊 (所有邏輯搬到這裡)
            EntityManager.Instance.ApplyPose(newObj.transform, data.pose);
            EntityManager.Instance.RegisterEntity(data.id, newObj);

            // 5. 掛載 Grabbable
            var grabbable = newObj.AddComponent<ObjGrabbable>();
            grabbable.entityId = data.id;

            // 6. 自動計算並加入 Collider (針對 newObj 處理)
            Renderer[] renderers = newObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && newObj.GetComponent<Collider>() == null)
            {
                var collider = newObj.AddComponent<BoxCollider>();
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

                collider.center = newObj.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;
            }
        }
        else
        {
            Debug.LogError($"[CreateProgObj] 下載失敗: {data.gltf.url}");
        }
    }
}