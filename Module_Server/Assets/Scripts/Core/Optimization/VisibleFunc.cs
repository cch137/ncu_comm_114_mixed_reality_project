using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Drawing;

using System.Collections.Generic;
using System.Linq;

public class VisibleFunc : MonoBehaviour
{
    [SerializeField]
    GameObject objcam;
    [SerializeField]
    Camera m_Camera;
    private bool Is_same;
    public Transform userpositon;
    private List<VolumetricUpdate> List_volumetric = new List<VolumetricUpdate>();
    private List<VolumetricUpdate> List_Visiable = new List<VolumetricUpdate>();
    private List<Camera> List_Objcam = new List<Camera>();
    // private List<VolumetricUpdate.ClusterInfo> clusters = VolumetricUpdate.GetClusterInfos();
    private List<GraphManager.ClusterInfo> clusters;
    private int childCount;
    private bool open = false;

    void Start()
    {
        childCount = objcam.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            List_Objcam.Add(new Camera());
        }
    }

    void Update()
    {
        if (open == false)
        {
            List_volumetric = GameObject.Find("ObjectManager").GetComponent<AllUpdateObject>().Object_list_get();
            for (int i = 0; i < List_volumetric.Count; i++)
            {
                List_Objcam[i] = objcam.transform.GetChild(i).gameObject.transform.GetChild(0).gameObject.transform.GetChild(0).GetComponent<Camera>();
                List_volumetric[i].originalsize_init(List_Objcam[i]);

                // 將物件添加到 GraphManager
                GraphManager.Instance.AddVolumetricObject(List_volumetric[i]);
            }
            open = true;
        }

        // 確保使用者物件被添加到 GraphManager
        if (userpositon != null)
        {
            GraphManager.Instance.AddUserObject(userpositon.gameObject);
        }

        // 更新圖形和分群
        GraphManager.Instance.UpdateGraph();
        clusters = GraphManager.Instance.GetClusterInfos();

        // Layer 控制：主相機拍攝同群所有物件，其他相機設為 Nothing
        UpdateCameraLayers(clusters);

        List_Visiable.Clear();
        for (int i = 0; i < List_volumetric.Count; i++)
        {
            List_volumetric[i].combinelayer.Clear();
            List_volumetric[i].combinelayer.Add(List_volumetric[i].name);

            // 檢查該物件是否為某群集的主相機
            bool isMainCamera = false;
            foreach (var cluster in clusters)
            {
                if (cluster.mainCamera == List_volumetric[i])
                {
                    isMainCamera = true;
                    // 檢查主相機是否在視野範圍內
                    if (List_volumetric[i].Visiable(m_Camera, List_Objcam[i]))
                    {
                        List_Visiable.Add(List_volumetric[i]);
                        // 主相機在視野內，確保其 GameObject 啟用
                        SetCameraActive(List_volumetric[i], true);
                    }
                    else
                    {
                        // 主相機不在視野內，關閉其相機
                        SetCameraActive(List_volumetric[i], false);
                    }
                    break;
                }
            }

            // 如果不是主相機，檢查是否在視野範圍內
            if (!isMainCamera && List_volumetric[i].Visiable(m_Camera, List_Objcam[i]))
            {
                List_Visiable.Add(List_volumetric[i]);
            }
        }

        List<VolumetricUpdate> sort_visiable = List_Visiable.OrderBy(o => o.Onframesize).ToList();
        for (int i = 0; i < sort_visiable.Count; i++)
        {
            Orth_cam(sort_visiable[i]);
        }
    }

    private void Orth_cam(VolumetricUpdate volumetric_object)
    {
        string new_volumetric_name = "Render_" + volumetric_object.name;
        Debug.Log($"new_volumetric_name:{new_volumetric_name}");
        for (int k = 0; k < List_Objcam.Count; k++)
        {
            Is_same = string.Equals(new_volumetric_name, List_Objcam[k].name);
            Debug.Log($"Is_same:{Is_same}");
            if (Is_same == true)
            {
                if (List_Objcam[k].orthographic == false)
                {
                    List_Objcam[k].orthographic = true;
                }
                break;
            }
        }
    }

    // Layer 控制：主相機拍攝同群所有物件，其他相機設為 Nothing
    private void UpdateCameraLayers(List<GraphManager.ClusterInfo> clusters)
    {
        // 設定主相機
        foreach (var clusterInfo in clusters)
        {
            if (clusterInfo.mainCamera != null)
            {
                // 找到主相機在 List_Objcam 的 index
                int mainIndex = -1;
                for (int i = 0; i < List_volumetric.Count; i++)
                {
                    if (List_volumetric[i] == clusterInfo.mainCamera)
                    {
                        mainIndex = i;
                        break;
                    }
                }
                if (mainIndex >= 0)
                {
                    int cullingMask = 0;
                    foreach (var obj in clusterInfo.objects)
                    {
                        // 讓主相機拍攝這個 Layer
                        List_Objcam[mainIndex].cullingMask |= 1 << LayerMask.NameToLayer(obj.name);
                        Debug.Log($"主相機 {List_Objcam[mainIndex].name} 開始拍攝 Layer: {obj.name}");

                        // 找到名稱為 obj.name 的相機，並設為 Nothing
                        for (int camIdx = 0; camIdx < List_Objcam.Count; camIdx++)
                        {
                            if (List_Objcam[camIdx].name == "Render_" + obj.name && List_Objcam[mainIndex].name != List_Objcam[camIdx].name)
                            {
                                List_Objcam[camIdx].cullingMask = 1 << LayerMask.NameToLayer("CloseCamera");
                                Debug.Log($"相機 {List_Objcam[camIdx].name} 設為只拍攝 CloseCamera Layer");
                                break;
                            }
                        }
                    }
                    Debug.Log($"主相機 {List_Objcam[mainIndex].name} 設定 cullingMask: {cullingMask}");
                }
            }
        }
        Debug.Log("=== clusterInfos 內容 ===");
        foreach (var cluster in clusters)
        {
            string objectNames = string.Join(", ", cluster.objects.Select(o => o.name));
            Debug.Log($"群集ID: {cluster.clusterId}，主相機: {cluster.mainCamera?.name ?? "無"}，物件: {objectNames}");
        }
        Debug.Log("=======================");
    }

    // 控制相機 GameObject 啟用/停用
    private void SetCameraActive(VolumetricUpdate obj, bool active)
    {
        string renderObjectName = "Render_" + obj.name;
        GameObject renderObject = GameObject.Find(renderObjectName);
        if (renderObject != null)
        {
            renderObject.SetActive(active);
        }
    }

    private bool IsOverlap(Rectangle rect1, Rectangle rect2)
    {
        double rect1_min_x = rect1.X;
        double rect1_min_y = rect1.Y;
        double rect1_max_x = rect1.X + rect1.Width;
        double rect1_max_y = rect1.Y + rect1.Height;

        double rect2_min_x = rect2.X;
        double rect2_min_y = rect2.Y;
        double rect2_max_x = rect2.X + rect2.Width;
        double rect2_max_y = rect2.Y + rect2.Height;

        Debug.Log(String.Format("(Rect1_min_x :{0}, Rect1_min_y :{1} , Rect1_max_x :{2} ,Rect1_max_y :{3})", rect1_min_x, rect1_min_y, rect1_max_x, rect1_max_y));
        Debug.Log(String.Format("(Rect2_min_x :{0}, Rect2_min_y :{1} , Rect2_max_x :{2} ,Rect2_max_y :{3})", rect2_min_x, rect2_min_y, rect2_max_x, rect2_max_y));

        if (rect1_min_x > rect2_max_x || rect1_max_x < rect2_min_x || rect1_min_y > rect2_max_y || rect1_max_y < rect2_min_y)
        {
            return false;
        }

        double Wid = Math.Min(rect1_max_x, rect2_max_x) - Math.Max(rect1_min_x, rect2_min_x);
        double Hei = Math.Min(rect1_max_y, rect2_max_y) - Math.Max(rect1_min_y, rect2_min_y);
        double overlaparea = Wid * Hei;
        double Rect1area = rect1.Width * rect1.Height;
        double overlapratio = overlaparea * 100 / Rect1area;

        if (overlapratio == 100)
        {
            return true;
        }
        return false;
    }
}

