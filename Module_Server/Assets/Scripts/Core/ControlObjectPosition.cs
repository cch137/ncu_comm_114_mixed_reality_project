using Microsoft.MixedReality.WebRTC.Unity;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectTransform
{
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public float RotW { get; set; }
    public float ScaleX { get; set; }
    public float ScaleY { get; set; }
    public float ScaleZ { get; set; }
}

public class ControlObjectPosition : MonoBehaviour
{
    [SerializeField]
    PeerConnection peerconnection;

    public static Dictionary<string, ObjectTransform> Object_control_center = new Dictionary<string, ObjectTransform>();
    public static string Received_object_json;
    public string object_json;
    string remoteID;
    bool name = false;
    public static bool myself = false;

    void Update()
    {
        if (name == false)
        {
            remoteID = GameObject.Find("NodeDssSignaler_Send").GetComponent<NodeDssSignaler>().GetRemoteID();
            name = true;
        }

        Received_object_json = peerconnection.GetInteractiveData();

        if (Received_object_json == object_json)
        {
            //Received_object_json = "";
            myself = false;
            Debug.Log($"{remoteID}");
            Debug.Log("Not received Interact Data");
        }
        else
        {
            object_json = Received_object_json;
            Object_control_center = JsonConvert.DeserializeObject<Dictionary<string, ObjectTransform>>(Received_object_json);
            Debug.Log(Received_object_json);
            myself = true;

            GameObject.Find("ObjectManager").GetComponent<AllUpdateObject>().Object_Update(Object_control_center);
        }
    }
}
