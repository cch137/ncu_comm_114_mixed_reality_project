using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Microsoft.MixedReality.WebRTC.Unity;

public class AllUpdateObject : MonoBehaviour
{

    private int childCount;
    private static List<VolumetricUpdate> List_volumetric = new List<VolumetricUpdate>();
    private string _controlobject;
    private ObjectTransform _controlobjectvalue;
    private double eps = 1;
    private float minPts = 2f;

    void Start()
    {
        childCount = transform.childCount;
        Debug.Log($"childCount: {childCount}");
        for (int i = 0; i < childCount; i++)
        {
            GameObject Getobject = transform.GetChild(i).gameObject.transform.GetChild(0).gameObject;
            List_volumetric.Add(new VolumetricUpdate());
            List_volumetric[i].object_init(Getobject);
        }
    }

    void Update()
    {

    }

    // Update is called once per frame
    public void Object_Update(Dictionary<string, ObjectTransform> Object_control_center)
    {
        foreach (KeyValuePair<string, ObjectTransform> objectname in Object_control_center)
        {
            _controlobject = objectname.Key;
            Debug.Log($"_controlobject: {_controlobject}");

            _controlobjectvalue = objectname.Value;
            int index = List_volumetric.FindIndex(x => x.name.Equals(_controlobject));
            Debug.Log($"List_volumetric: {List_volumetric}");
            Debug.Log($"index: {index}");

            List_volumetric[index].Object_update(_controlobjectvalue);
        }
    }

    public bool Object_camerachange(string _controlobject)
    {
        int index = List_volumetric.FindIndex(x => x.name.Equals(_controlobject));
        return List_volumetric[index].GetCamera_change();
    }

    public float Object_ratio(string _controlobject)
    {
        int index = List_volumetric.FindIndex(x => x.name.Equals(_controlobject));
        return List_volumetric[index].GetScaleratio();
    }

    public List<VolumetricUpdate> Object_list_get()
    {
        return List_volumetric;
    }
}
