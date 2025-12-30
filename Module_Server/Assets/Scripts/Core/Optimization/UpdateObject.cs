using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;


public class UpdateObject : MonoBehaviour
{


    private int childCount;
    private List<VolumetricObject> List_volumetric = new List<VolumetricObject>();
    string remoteID;
    private string _controlobject;
    private ObjectTransform _controlobjectvalue;

    void Start()
    {
        remoteID = GameObject.Find("NodeDssSignaler_Send").GetComponent<NodeDssSignaler>().GetRemoteID();
        childCount = GameObject.Find("ObjectManager").transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = GameObject.Find("ObjectManager").transform.GetChild(i).gameObject;
            List_volumetric.Add(new VolumetricObject());
            List_volumetric[i].object_init(Getobject, remoteID);
        }
    }

    void Update()
    {
        if (ControlObjectPosition.Received_object_json != "")
        {
            foreach (KeyValuePair<string, ObjectTransform> objectname in ControlObjectPosition.Object_control_center)
            {
                _controlobject = objectname.Key;
                _controlobjectvalue = objectname.Value;
                int index = List_volumetric.FindIndex(x => x.name.Equals(_controlobject));
                VolumetricObject volumetric = List_volumetric[index];
                volumetric.Object_update(_controlobjectvalue);
                List_volumetric[index] = volumetric;
            }
        }

    }
}
