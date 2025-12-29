using UnityEngine;

public class ServerGameLogic : MonoBehaviour
{
    // 宣告管理器
    private WorldStateManager<BaseObjectState> _worldState = new WorldStateManager<BaseObjectState>();

    void Update()
    {
        // 假設這是在 Server 端接收到 Client 的數據
        string uuid = "player-001";

        // 情況 A：直接從 Unity 物件獲取 (最常用)
        // transform.rotation 本身就是 Quaternion
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        // 情況 B：如果你手邊只有歐拉角 (例如 (0, 90, 0))
        // 使用 Quaternion.Euler 轉換
        // Quaternion calculatedRot = Quaternion.Euler(0, 90, 0);

        // 建立狀態物件
        BaseObjectState state = new BaseObjectState(uuid, currentPos, currentRot);

        // 存入 ConcurrentDictionary
        _worldState.UpdateObject(uuid, state);
    }

    void VerifyData()
    {
        var obj = _worldState.GetObject("player-001");
        if (obj != null)
        {
            // 取出數據時，可以直接使用
            // obj.Rotation.x, obj.Rotation.y, obj.Rotation.z, obj.Rotation.w
            Debug.Log($"目前的旋轉 W 分量: {obj.Rotation.w}");
        }
    }
}
