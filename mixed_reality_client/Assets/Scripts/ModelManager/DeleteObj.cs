using UnityEngine;

public class DeleteObj : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnDelEntity += HandleDelEntity;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.OnDelEntity -= HandleDelEntity;
    }

    private void HandleDelEntity(DeleteEntityData data)
    {
        EntityManager.Instance.RemoveEntity(data.id);
    }
}