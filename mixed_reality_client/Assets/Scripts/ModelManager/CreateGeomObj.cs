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

    private void HandleCreateGeom(EntityBaseData data)
    {
        // ネΘ碭砰
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = $"GeomObj_{data.id}";

        // 砞﹚竚
        EntityManager.Instance.ApplyPose(obj.transform, data.pose);

        // 爹
        EntityManager.Instance.RegisterEntity(data.id, obj);

        // 」」」 穝糤笆本更 NetworkGrabbable 」」」
        var grabbable = obj.AddComponent<ObjGrabbable>();
        grabbable.entityId = data.id; // 璶

        Debug.Log($"[CreateGeomObj] ネΘ本更竲セ ID: {data.id}");
    }
}