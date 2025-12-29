using UnityEngine;
using System.Collections.Generic;

public class EntityManager : MonoBehaviour
{
    public static EntityManager Instance;

    // 儲存所有連線物件的字典：Key = Entity ID, Value = GameObject
    private Dictionary<string, GameObject> spawnedEntities = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 註冊一個新物件到字典中
    /// </summary>
    public void RegisterEntity(string id, GameObject obj)
    {
        if (spawnedEntities.ContainsKey(id))
        {
            Debug.LogWarning($"[EntityManager] ID {id} 已存在，正在銷毀舊物件並替換...");
            Destroy(spawnedEntities[id]);
            spawnedEntities.Remove(id);
        }

        // 設定物件名稱方便 Debug
        obj.name = $"{obj.name}_{id}";
        spawnedEntities.Add(id, obj);
    }

    /// <summary>
    /// 取得物件
    /// </summary>
    public GameObject GetEntity(string id)
    {
        if (spawnedEntities.TryGetValue(id, out GameObject obj))
        {
            return obj;
        }
        return null; // 找不到
    }

    /// <summary>
    /// 移除並銷毀物件
    /// </summary>
    public void RemoveEntity(string id)
    {
        if (spawnedEntities.TryGetValue(id, out GameObject obj))
        {
            Destroy(obj);
            spawnedEntities.Remove(id);
            Debug.Log($"[EntityManager] 已移除物件: {id}");
        }
        else
        {
            Debug.LogWarning($"[EntityManager] 找不到要刪除的 ID: {id}");
        }
    }

    // ---------------------------------------------------------
    //  請將這一段加在這裡 (RemoveEntity 下方)
    // ---------------------------------------------------------
    public void ClearAll()
    {
        foreach (var obj in spawnedEntities.Values)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedEntities.Clear();
        Debug.Log("[EntityManager] 已清空所有網路物件");
    }
    // ---------------------------------------------------------

    /// <summary>
    /// 工具：將 PoseData 轉為 Unity 的 Vector3/Quaternion
    /// </summary>
    public void ApplyPose(Transform target, PoseData pose)
    {
        if (pose == null) return;

        if (pose.pos != null && pose.pos.Length == 3)
        {
            target.position = new Vector3(pose.pos[0], pose.pos[1], pose.pos[2]);
        }

        if (pose.rot != null && pose.rot.Length == 4)
        {
            target.rotation = new Quaternion(pose.rot[0], pose.rot[1], pose.rot[2], pose.rot[3]);
        }
    }
}