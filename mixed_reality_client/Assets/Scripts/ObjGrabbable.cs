using UnityEngine;

public class ObjGrabbable : MonoBehaviour
{
    [Header("物件 ID (通常由 Spawn 生成時自動填入)")]
    public string entityId;

    [Header("發送頻率")]
    public float updateRate = 0.1f;

    private bool isGrabbed = false;
    private float timer;

    // 用來記錄這物件是否屬於「我」。如果是我控制的，就不要聽 Server 的座標更新
    public bool IsOwnedByMe => isGrabbed;

    // --- 外部呼叫介面 (給 VR Grab Event 或 滑鼠點擊使用) ---

    // 1. 當玩家「抓起」物件時呼叫此方法
    public void OnGrabStart()
    {
        if (string.IsNullOrEmpty(entityId)) return;

        isGrabbed = true;

        // 準備資料
        EntityControlData data = new EntityControlData { id = entityId };

        // ★★★ 修改處：明確指定泛型 <EntityControlData> ★★★
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<EntityControlData>("ClaimEntity", data);
        }

        Debug.Log($"[互動] Claim 物件: {entityId}");
    }

    // 2. 當玩家「放開」物件時呼叫此方法
    public void OnGrabEnd()
    {
        if (string.IsNullOrEmpty(entityId)) return;

        isGrabbed = false;

        // 準備資料
        EntityControlData data = new EntityControlData { id = entityId };

        // ★★★ 修改處：明確指定泛型 <EntityControlData> ★★★
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<EntityControlData>("ReleaseEntity", data);
        }

        Debug.Log($"[互動] Release 物件: {entityId}");
    }

    // --- 3. 持續更新迴圈 (UpdateEntity) ---
    private void Update()
    {
        // 只有在「被抓取中」且「NetworkManager 活著」時才發送
        if (isGrabbed && NetworkManager.Instance != null)
        {
            timer += Time.deltaTime;
            if (timer >= updateRate)
            {
                SendTransformUpdate();
                timer = 0;
            }
        }
    }

    private void SendTransformUpdate()
    {
        // 準備資料
        EntityBaseData data = new EntityBaseData
        {
            id = entityId,
            pose = new PoseData
            {
                pos = new float[] { transform.position.x, transform.position.y, transform.position.z },
                rot = new float[] { transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w }
            }
        };

        // ★★★ 修改處：明確指定泛型 <EntityBaseData> ★★★
        // 這裡不需要額外檢查 NetworkManager.Instance，因為 Update 裡已經檢查過了
        NetworkManager.Instance.Send<EntityBaseData>("UpdateEntity", data);
    }
}