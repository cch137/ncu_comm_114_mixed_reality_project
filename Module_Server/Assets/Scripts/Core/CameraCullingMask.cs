using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraCullingMask : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // 找到所有名為 Object_camera 的 GameObject
        GameObject cameraParent = GameObject.Find("Object_Camera");
        if (cameraParent == null)
        {
            Debug.Log("Cannot find GameObject named Object_Camera");
            return;
        }

        // 遍歷所有子物件
        foreach (Transform child in cameraParent.transform)
        { 
            Debug.LogError($"turn{child.name}");
            // 找到子物件下的 Render_Object
            GameObject renderObject = child.Find("Media_render_" + child.name).gameObject;
            if (renderObject == null)
            {
                Debug.LogError($"Cannot find Render_Object in {child.name}");
                continue;
            }
            Debug.LogError($"turn{renderObject.name}");
            // 獲取 Render_Object 的 Camera 组件
            Camera camera = renderObject.gameObject.transform.GetChild(0).GetComponent<Camera>();
            if (camera == null)
            {
                Debug.LogError($"No Camera component found on {renderObject.name}");
                continue;
            }
            Debug.LogError($"turn{camera.name}");
            // 設置 Culling Mask
            camera.cullingMask = 1 << LayerMask.NameToLayer(child.name);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
