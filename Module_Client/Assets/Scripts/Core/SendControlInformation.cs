using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;

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

public class SendControlInformation : MonoBehaviour
{
    [SerializeField]
    PeerConnection peerConnection;

    private string json_object_control;
    private int childCount;
    private Dictionary<string, ObjectTransform> object_control = new Dictionary<string, ObjectTransform>();
    private Dictionary<string, ObjectTransform> send_control = new Dictionary<string, ObjectTransform>();

    void Update()
    {
        childCount = transform.childCount;
        json_object_control = "";
        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = transform.GetChild(i).gameObject;
            UpdateObjectDatatoDict(object_control, Getobject);
        }

        if (send_control.Count != 0)
        {
            json_object_control = JsonConvert.SerializeObject(send_control);
            send_control.Clear();
            if (peerConnection != null)
            {
                peerConnection.UseDataChannel("Interact", json_object_control);
            }
            else
            {
                Debug.LogWarning("SendControlInformation: PeerConnection is null - cannot send 'interact' data. Assign a PeerConnection in the inspector or add one to the GameObject.");
            }
        }
    }

    private void Awake()
    {
        if (peerConnection == null)
        {
            peerConnection = GetComponent<PeerConnection>();
        }
        if (peerConnection == null)
        {
            Debug.LogWarning("SendControlInformation Awake: No PeerConnection found on this GameObject or assigned in inspector.");
        }
    }

    private void UpdateObjectDatatoDict(Dictionary<string, ObjectTransform> dict, GameObject Object)
    {
        string key = Object.name;
        if (dict.TryGetValue(key, out _))
        {
            ObjectTransform newdata = GetObjectData(Object.transform);
            bool is_same = Comparenotsame(dict[key], newdata);
            if (is_same)
            {
                dict[key] = GetObjectData(Object.transform);
                AddObjectDatatoSend(send_control, key, dict[key]);
            }
        }
        else
        {
            dict.Add(key, GetObjectData(Object.transform));
            AddObjectDatatoSend(send_control, key, dict[key]);
        }
    }

    private static bool Comparenotsame(ObjectTransform objectone, ObjectTransform objecttwo)
    {
        if (objectone.PosX != objecttwo.PosX || objectone.PosY != objecttwo.PosY || objectone.PosZ != objecttwo.PosZ)
        {
            return true;
        }
        else if (objectone.RotX != objecttwo.RotX)
        {
            return true;
        }
        else if (objectone.ScaleX != objecttwo.ScaleX)
        {
            return true;
        }
        return false;
    }


    private static void AddObjectDatatoSend(Dictionary<string, ObjectTransform> Send_control0, string key, ObjectTransform value)
    {
        Send_control0.Add(key, value);
    }

    private ObjectTransform GetObjectData(Transform Object)
    {
        Vector3 position = Object.position;
        Quaternion rotation = Object.rotation;
        Vector3 Scale = Object.localScale;

        ObjectTransform lst_object_transform = new ObjectTransform()
        {
            PosX = position.x,
            PosY = position.y,
            PosZ = position.z,
            RotX = rotation.x,
            RotY = rotation.y,
            RotZ = rotation.z,
            RotW = rotation.w,
            ScaleX = Scale.x,
            ScaleY = Scale.y,
            ScaleZ = Scale.z
        };
        return lst_object_transform;
    }

    public void server_change()
    {
        childCount = transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = transform.GetChild(i).gameObject;
            UpdateObjectDatatoDict(object_control, Getobject);
        }
        send_control.Clear();
    }
}
