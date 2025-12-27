using UnityEngine; // 引用 Unity 數學庫
using System;

[Serializable]
public class BaseObjectState
{
    public string Uuid { get; private set; }

    // 位置：x, y, z
    public Vector3 Position;

    // 旋轉：改用 Quaternion (x, y, z, w)
    public Quaternion Rotation;

    public long LastUpdateTime;

    // 建構子：傳入 Quaternion
    public BaseObjectState(string uuid, Vector3 pos, Quaternion rot)
    {
        this.Uuid = uuid;
        this.Position = pos;
        this.Rotation = rot;
        this.LastUpdateTime = DateTime.UtcNow.Ticks;
    }

    // 更新方法也同步修改
    public virtual void UpdateState(Vector3 newPos, Quaternion newRot)
    {
        this.Position = newPos;
        this.Rotation = newRot;
        this.LastUpdateTime = DateTime.UtcNow.Ticks;
    }
}

