using UnityEngine;

public class CreateAnchorObj : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnCreateAnchor += HandleCreateAnchor;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnCreateAnchor -= HandleCreateAnchor;
    }

    private void HandleCreateAnchor(EntityData data)
    {
        GameObject obj = new GameObject("Anchor");

        // 為了 Debug 看得見，加一個小圖示或 Gizmo
        // 實際專案可能不需要

        EntityManager.Instance.ApplyPose(obj.transform, data.pose);
        EntityManager.Instance.RegisterEntity(data.id, obj);

        Debug.Log($"[CreateAnchorObj] 生成錨點 ID: {data.id}");
    }
}