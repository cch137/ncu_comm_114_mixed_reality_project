using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // 引用 XR 套件
using UnityEngine.XR.Interaction.Toolkit.Interactables; // 新版 Unity 6/XRIT 3.x 可能需要這個，若報錯可刪除
using GLTFast;
using System.Threading.Tasks;

public class CreateProgObj : MonoBehaviour
{
    [Header("生成設定")]
    [Tooltip("勾選：物件受重力影響、落地、可被丟擲。\n不勾選：物件會懸浮、抓取放開後會停在原處(無重力)。")]
    public bool usePhysics = true; // ★★★ 新增這個公開變數 ★★★

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

        // 1. 下載 GLTF
        var gltf = new GltfImport();
        bool success = await gltf.Load(data.gltf.url);

        // 如果下載過程中腳本被銷毀，則停止
        if (this == null) return;

        if (success)
        {
            // 2. 建立空的父物件
            GameObject newObj = new GameObject($"ProgObj_{data.gltf.name}");

            // 3. 將模型實例化到父物件下
            await gltf.InstantiateMainSceneAsync(newObj.transform);

            // 安全檢查
            if (this == null || newObj == null) return;

            // 4. 設定位置與註冊實體
            EntityManager.Instance.ApplyPose(newObj.transform, data.pose);
            EntityManager.Instance.RegisterEntity(data.id, newObj);

            // ==========================================
            //       以下是整合後的自動化設定流程
            // ==========================================

            // 5. 設定 Collider (碰撞體)
            Renderer[] renderers = newObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                if (newObj.GetComponent<Collider>() == null)
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
                Debug.LogWarning($"[CreateProgObj] 物件 {newObj.name} 沒有 Renderer，無法自動添加 Collider");
            }

            // 6. 設定 Rigidbody (物理剛體)
            Rigidbody rb = newObj.GetComponent<Rigidbody>();
            if (rb == null) rb = newObj.AddComponent<Rigidbody>();

            rb.mass = 1.0f;
            rb.linearDamping = 0.5f;   // Unity 6 新版寫法
            rb.angularDamping = 0.05f; // Unity 6 新版寫法

            // ★★★ 根據 public bool 設定物理狀態 ★★★
            if (usePhysics)
            {
                rb.useGravity = true;    // 開啟重力
                rb.isKinematic = false;  // 由物理引擎接管 (會掉落)
            }
            else
            {
                rb.useGravity = false;   // 關閉重力
                rb.isKinematic = true;   // 設為 Kinematic (懸浮，不會被撞飛)
            }

            // 7. 設定 XR Grab Interactable
            XRGrabInteractable grab = newObj.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = newObj.AddComponent<XRGrabInteractable>();

            grab.movementType = XRBaseInteractable.MovementType.Kinematic;

            // ★★★ 如果不使用物理，就不開啟投擲功能，避免怪異行為 ★★★
            grab.throwOnDetach = usePhysics;
            grab.throwSmoothingDuration = 0.25f;

            // 確保 HoloLens/Quest 互動層級正確
            grab.interactionLayers = InteractionLayerMask.GetMask("Default");

            // 8. 掛載同步腳本 ObjGrabbable 並綁定事件
            var syncScript = newObj.AddComponent<ObjGrabbable>();
            syncScript.entityId = data.id;

            grab.selectEntered.AddListener((args) => {
                syncScript.OnGrabStart();
                Debug.Log($"[CreateProgObj] {data.id} 被抓取");
            });

            grab.selectExited.AddListener((args) => {
                syncScript.OnGrabEnd();
                Debug.Log($"[CreateProgObj] {data.id} 被放開");
            });

            Debug.Log($"[CreateProgObj] 物件 {data.id} 生成完成 (Physics: {usePhysics})");
        }
        else
        {
            Debug.LogError($"[CreateProgObj] 下載失敗: {data.gltf.url}");
        }
    }
}