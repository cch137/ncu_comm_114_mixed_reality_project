using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;
using Microsoft.MixedReality.WebRTC.Unity;

public class UpdataMultiUserPosition : MonoBehaviour
{
    [SerializeField]
    PeerConnection PeerConnection;

    private string json_object_control;
    private int childCount;
    private Dictionary<string, ObjectTransform> object_control = new Dictionary<string, ObjectTransform>();
    private Dictionary<string, ObjectTransform> send_object_position = new Dictionary<string, ObjectTransform>();
    private bool change = false;

    void Update()
    {
        childCount = GameObject.Find("ObjectManager").transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = GameObject.Find("ObjectManager").transform.GetChild(i).gameObject;
            UpdateObjectDatatoDict(object_control, Getobject);
        }
        if (ControlObjectPosition.myself == false)
        {
            json_object_control = "";

            Debug.Log($"{send_object_position.Count}");
            if (send_object_position.Count != 0)
            {
                json_object_control = JsonConvert.SerializeObject(send_object_position);
                Debug.Log($"send_object_position: {send_object_position}");
                send_object_position.Clear();
                PeerConnection.UseDataChannel("Interact", json_object_control);
            }
        }
        else
        {
            send_object_position.Clear();
        }
    }

    private void UpdateObjectDatatoDict(Dictionary<string, ObjectTransform> dict, GameObject Object)
    {
        string key = Object.name;
        if (dict.TryGetValue(key, out _))
        {
            ObjectTransform newdata = GetObjectData(Object.transform.GetChild(0));
            bool is_notsame = Comparenotsame(dict[key], newdata);
            if (is_notsame)
            {
                dict[key] = GetObjectData(Object.transform.GetChild(0));
                AddObjectDatatoSend(send_object_position, key, dict[key]);
            }
        }
        else
        {
            dict.Add(key, GetObjectData(Object.transform.GetChild(0)));
            AddObjectDatatoSend(send_object_position, key, dict[key]);
        }
    }

    private bool Comparenotsame(ObjectTransform objectone, ObjectTransform objecttwo)
    {
        if (objectone.PosX != objecttwo.PosX)
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


    private void AddObjectDatatoSend(Dictionary<string, ObjectTransform> send_object_position, string key, ObjectTransform value)
    {
        send_object_position.Add(key, value);
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
}
