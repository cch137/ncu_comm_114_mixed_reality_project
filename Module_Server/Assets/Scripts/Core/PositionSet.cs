using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionSet : MonoBehaviour
{
    [SerializeField] public Vector3 MovementMinBoundary;
    [SerializeField] public Vector3 MovementMaxBoundary;
    
    public GameObject[] big3dModels;
    public GameObject[] small3dModels;

    public Vector3[] big3dModelPositions;
    public Vector3[] small3dModelPositions;

   
    // �t�שM��V���]�w�A�i�H�w��C�Ӫ����W�]�w
    [System.Serializable]
    public struct MovementSettings
    {
        [SerializeField] public Vector3 direction;
        [SerializeField] public float speed;
        //[SerializeField] public float acceleration;
        //[SerializeField] public float deceleration;
        
    }

    public MovementSettings[] big3dModelMovement;
    public MovementSettings[] small3dModelMovement;

    private Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, float> lastUpdateTimes = new Dictionary<string, float>();

    private void Start()
    {
        if (big3dModelPositions.Length != big3dModels.Length || small3dModelPositions.Length != small3dModels.Length ||
            big3dModelMovement.Length != big3dModels.Length || small3dModelMovement.Length != small3dModels.Length)
        {
            Debug.LogError("陣列長度不一致");
            return;
        }

        SetPositions(big3dModels, big3dModelPositions, big3dModelMovement);
        SetPositions(small3dModels, small3dModelPositions, small3dModelMovement);

        // 初始化位置記錄
        foreach (var obj in big3dModels)
        {
            lastPositions[obj.name] = obj.transform.position;
            lastUpdateTimes[obj.name] = Time.time;
        }
        foreach (var obj in small3dModels)
        {
            lastPositions[obj.name] = obj.transform.position;
            lastUpdateTimes[obj.name] = Time.time;
        }
    }

    private void Update()
    {
        UpdateMovementInfo();
    }

    private void UpdateMovementInfo()
    {
        // 更新大物件的移動資訊
        for (int i = 0; i < big3dModels.Length; i++)
        {
            UpdateObjectMovement(big3dModels[i], ref big3dModelMovement[i]);
        }

        // 更新小物件的移動資訊
        for (int i = 0; i < small3dModels.Length; i++)
        {
            UpdateObjectMovement(small3dModels[i], ref small3dModelMovement[i]);
        }
    }

    private void UpdateObjectMovement(GameObject obj, ref MovementSettings movementSettings)
    {
        if (!lastPositions.ContainsKey(obj.name))
        {
            lastPositions[obj.name] = obj.transform.position;
            lastUpdateTimes[obj.name] = Time.time;
            return;
        }

        float deltaTime = Time.time - lastUpdateTimes[obj.name];
        if (deltaTime > 0)
        {
            Vector3 currentPosition = obj.transform.position;
            Vector3 displacement = currentPosition - lastPositions[obj.name];
            
            // 更新速度
            movementSettings.speed = displacement.magnitude / deltaTime;
            
            // 更新方向
            if (displacement.magnitude > 0.001f)  // 避免除以零
            {
                movementSettings.direction = displacement.normalized;
            }

            // 更新記錄
            lastPositions[obj.name] = currentPosition;
            lastUpdateTimes[obj.name] = Time.time;

            // 輸出除錯資訊
            Debug.Log($"物件 {obj.name} 的移動資訊更新：");
            Debug.Log($"- 速度: {movementSettings.speed}");
            Debug.Log($"- 方向: {movementSettings.direction}");
        }
    }

    private void SetPositions(GameObject[] models, Vector3[] positions, MovementSettings[] movementSettings)
    {
        for (int i = 0; i < models.Length; i++)
        {
            models[i].transform.position = positions[i];

            // ���C�Ӫ���K�[ LinearMovement script�A�ó]�w���P���t��
            LinearMovement movement = models[i].AddComponent<LinearMovement>();
            movement.direction = movementSettings[i].direction;
            movement.speed = movementSettings[i].speed;
            //movement.acceleration = movementSettings[i].acceleration;
            //movement.deceleration = movementSettings[i].deceleration;
            movement.minBoundary = MovementMinBoundary;
            movement.maxBoundary = MovementMaxBoundary;
        }
    }
}