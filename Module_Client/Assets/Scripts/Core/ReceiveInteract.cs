using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;

public class ReceiveInteract : MonoBehaviour
{
    [SerializeField]
    PeerConnection peerconnection;

    public static Dictionary<string, ObjectTransform> Object_control_center = new Dictionary<string, ObjectTransform>();
    public static string Received_object_json;
    public string object_json;

    void Update()
    {
        Received_object_json = peerconnection.GetInteractiveData();

        if (Received_object_json == object_json)
        {
            Received_object_json = "";
        }
        else
        {
            object_json = Received_object_json;
            Object_control_center = JsonConvert.DeserializeObject<Dictionary<string, ObjectTransform>>(Received_object_json);
            Debug.Log(Received_object_json);
        }
    }
}
