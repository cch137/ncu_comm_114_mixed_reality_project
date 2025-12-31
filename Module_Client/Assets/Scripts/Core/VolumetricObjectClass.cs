using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;

public class VolumetricObject
{
    private GameObject objPos;
    private GameObject boxOfObj;
    private GameObject videoOfObj;
    public string Name;
    private int _time = 0;
    public Vector3 position;
    private Vector3 new_localscale;
    private Vector3 old_localscale;

    public void ObjectInit(GameObject gameObject)
    {
        objPos = gameObject;
        Name = gameObject.name;
        position = gameObject.transform.position;
    }

    public void ObjectUpdate(ObjectTransform new_object_value)
    {
        Vector3 objPosition = new Vector3(new_object_value.PosX, new_object_value.PosY, new_object_value.PosZ);
        objPos.transform.position = objPosition;
        position = objPos.transform.position;

        if (_time == 0)
        {
            old_localscale = new Vector3(new_object_value.ScaleX, new_object_value.ScaleY, new_object_value.ScaleZ);
            _time += 1;
        }
        else
        {
            new_localscale = new Vector3(new_object_value.ScaleX, new_object_value.ScaleY, new_object_value.ScaleZ);
            if (new_localscale != old_localscale)
            {
                float Scaleratio = new_localscale.x / old_localscale.x;
                objPos.transform.localScale = objPos.transform.localScale * Scaleratio;
            }
            old_localscale = new_localscale;
        }
    }
}
