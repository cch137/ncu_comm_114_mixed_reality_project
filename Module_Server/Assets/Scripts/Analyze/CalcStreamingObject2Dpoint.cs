using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;

public class CalcStreamingObject2Dpoint : MonoBehaviour
{
    [SerializeField]
    PeerConnection PeerConnection;

    [SerializeField]
    Camera streamingcam;

    private GameObject Meshposition;
    private string json_reporjection_data_2D;
    private Vector2 originalsize;
    private Camera targetCamera;
    private List<GraphManager.ClusterInfo> clusters;

    private void Start()
    {
        Meshposition = GameObject.Find("ObjectManager").transform.GetChild(2).transform.GetChild(0).gameObject;
    }

    private void Update()
    {
        if (GraphManager.Instance != null)
        {
            GraphManager.Instance.UpdateGraph();
            clusters = GraphManager.Instance.GetClusterInfos();
            UpdateTargetCamera();
        }

        if (targetCamera != null)
        {
            json_reporjection_data_2D = Datatobetransmitted(Meshposition, targetCamera);
            if (PeerConnection == null)
            {
                Debug.LogWarning("CalcStreamingObject2Dpoint.Update: PeerConnection is not assigned. Skipping data channel send.");
            }
            else
            {
                try
                {
                    PeerConnection.UseDataChannel("SendtoClient", json_reporjection_data_2D);
                    Debug.Log($"傳送資料 - 物件: {Meshposition.name}, 相機: {targetCamera.name}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"CalcStreamingObject2Dpoint.Update: Exception while sending data channel: {ex}");
                }
            }
        }
    }

    private void UpdateTargetCamera()
    {
        string layerName = Meshposition.name;
        Debug.Log($"尋找 layer: {layerName} 的主相機");

        foreach (var cluster in clusters)
        {
            if (cluster.mainCamera != null)
            {
                bool containsLayer = cluster.objects.Any(obj => obj.name == layerName);
                if (containsLayer)
                {
                    string renderCamName = $"Render_{cluster.mainCamera.name}";
                    GameObject renderCamObj = GameObject.Find(renderCamName);
                    if (renderCamObj != null)
                    {
                        Camera cam = renderCamObj.GetComponent<Camera>();
                        if (cam != null)
                        {
                            targetCamera = cam;
                            Debug.Log($"找到主相機: {cam.name} 用於 layer: {layerName}");
                            return;
                        }
                    }
                }
            }
        }

        targetCamera = streamingcam;
        Debug.Log($"未找到主相機，使用預設相機: {streamingcam.name}");
    }

    private string Datatobetransmitted(GameObject objectname, Camera targetCamera)
    {
        Vector3 Object_OriginalPosition = objectname.transform.position;
        Vector3 Object_ScreenPosition = GetObjectScreenPoint(objectname, targetCamera);
        originalsize.x = targetCamera.rect.width * 960;
        originalsize.y = targetCamera.rect.height * 960;

        Caculate_Reprojection_2D object_reprojection_data = new Caculate_Reprojection_2D()
        {
            Object_OriginalPosition_x = Object_OriginalPosition.x,
            Object_OriginalPosition_y = Object_OriginalPosition.y,
            Object_OriginalPosition_z = Object_OriginalPosition.z,
            Object_Screen_norm_x = Object_ScreenPosition.x / originalsize.x,
            Object_Screen_norm_y = Object_ScreenPosition.y / originalsize.y,
            Object_ScreenPosition_z = Object_ScreenPosition.z
        };

        string data = JsonConvert.SerializeObject(object_reprojection_data);
        return data;
    }

    private Vector3 GetObjectScreenPoint(GameObject objectname, Camera targetCamera)
    {
        Vector3 Screentpoint = targetCamera.WorldToScreenPoint(objectname.transform.position);
        return Screentpoint;
    }
}
