using UnityEngine;

public class CreateGeomObj : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnCreateGeomObj += HandleCreateGeom;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnCreateGeomObj -= HandleCreateGeom;
    }

    private void HandleCreateGeom(EntityData data)
    {
        // 生成幾何體
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = $"GeomObj_{data.id}";

        // 設定位置
        EntityManager.Instance.ApplyPose(obj.transform, data.pose);

        // 註冊
        EntityManager.Instance.RegisterEntity(data.id, obj);

        // ★★★ 新增：自動掛載 NetworkGrabbable ★★★
        var grabbable = obj.AddComponent<ObjGrabbable>();
        grabbable.entityId = data.id; // 重要！

        Debug.Log($"[CreateGeomObj] 生成並掛載腳本 ID: {data.id}");
    }
}