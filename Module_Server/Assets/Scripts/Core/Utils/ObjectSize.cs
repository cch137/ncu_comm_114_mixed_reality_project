using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSize : MonoBehaviour
{
    private Renderer objRenderer; // 使用 Renderer
    public float dis_obj_cam;

    void Start()
    {
        // 找到 Renderer 组件
        objRenderer = GetComponent<Renderer>();

        // 是否找到 Renderer
        if (objRenderer == null)
        {
            Debug.LogError("ObjectSize: Renderer component not found on this GameObject.");
            return;
        }

        // 打印初始尺寸
        Debug.Log("Initial Size X: " + objRenderer.bounds.size.x);
        Debug.Log("Initial Size Y: " + objRenderer.bounds.size.y);
        Debug.Log("Initial Size Z: " + objRenderer.bounds.size.z);

        // 對角線長度
        float diagonalLength = CalculateDiagonalLength(objRenderer.bounds);
        Debug.Log("Initial Diagonal Length: " + diagonalLength);

    }
    private float CalculateDiagonalLength(Bounds bounds)
    {
        return Mathf.Sqrt(
            Mathf.Pow(bounds.size.x, 2) +
            Mathf.Pow(bounds.size.y, 2) +
            Mathf.Pow(bounds.size.z, 2)
        );
    }

    public float obj_cam_distance()
    {
        if (objRenderer == null) return 0f;

        dis_obj_cam = CalculateDiagonalLength(objRenderer.bounds);
        return dis_obj_cam;
    }
    void Update()
    {

    }
}
