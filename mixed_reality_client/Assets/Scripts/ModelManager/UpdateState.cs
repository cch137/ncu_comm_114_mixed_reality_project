using UnityEngine;

public class UpdateState : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnUpdateEntity += HandleUpdateEntity;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnUpdateEntity -= HandleUpdateEntity;
    }

    private void HandleUpdateEntity(EntityData data)
    {
        GameObject target = EntityManager.Instance.GetEntity(data.id);

        if (target != null)
        {
            // ★★★ 關鍵修正 ★★★
            // 檢查該物件是否有 NetworkGrabbable 元件，且是否正在被我們抓取
            var grabbable = target.GetComponent<ObjGrabbable>();

            // 如果 "是我正在抓的"，就忽略 Server 的座標更新 (Local Prediction)
            if (grabbable != null && grabbable.IsOwnedByMe)
            {
                return;
            }

            // 否則，正常同步 Server 的位置
            EntityManager.Instance.ApplyPose(target.transform, data.pose);
        }
    }
}