using System.Collections.Concurrent;
using System.Collections.Generic;

// 這段程式碼與之前完全相同，可以直接沿用
public class WorldStateManager<T> where T : BaseObjectState
{
    private ConcurrentDictionary<string, T> _objectStore;

    public WorldStateManager()
    {
        _objectStore = new ConcurrentDictionary<string, T>();
    }

    public void UpdateObject(string uuid, T newState)
    {
        _objectStore.AddOrUpdate(
            uuid,
            newState,
            (key, oldValue) =>
            {
                // 這裡會呼叫 BaseObjectState 裡更新後的 UpdateState 方法
                oldValue.UpdateState(newState.Position, newState.Rotation);
                return oldValue;
            }
        );
    }

    public T GetObject(string uuid)
    {
        if (_objectStore.TryGetValue(uuid, out T state)) return state;
        return null;
    }

    public bool RemoveObject(string uuid)
    {
        return _objectStore.TryRemove(uuid, out _);
    }
}
