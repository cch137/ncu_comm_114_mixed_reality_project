
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdatePosition : MonoBehaviour
{
    private int childCount;
    private List<VolumetricObject> List_volumetric = new List<VolumetricObject>();
    private string _controlobject;
    private ObjectTransform _controlobjectvalue;

    void Start()
    {
        childCount = GameObject.Find("Object_control_center").transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = GameObject.Find("Object_control_center").transform.GetChild(i).gameObject;
            List_volumetric.Add(new VolumetricObject());
            List_volumetric[i].ObjectInit(Getobject);
        }
    }
    void Update()
    {

        if (ReceiveInteract.Received_object_json != "")
        {
            foreach (KeyValuePair<string, ObjectTransform> objectname in ReceiveInteract.Object_control_center)
            {
                _controlobject = objectname.Key;

                _controlobjectvalue = objectname.Value;
                int index = List_volumetric.FindIndex(x => x.Name.Equals(_controlobject));

                VolumetricObject volumetric = List_volumetric[index];
                volumetric.ObjectUpdate(_controlobjectvalue);
                List_volumetric[index] = volumetric;

            }

            GameObject.Find("Object_control_center").GetComponent<SendControlInformation>().server_change();
        }



    }

}
