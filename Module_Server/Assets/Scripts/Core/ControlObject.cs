using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlObject : MonoBehaviour
{
    [SerializeField]
    Transform User_position;

    private GameObject OBJ_True;
    private GameObject Box_of_OBJ;
    private GameObject Cam_of_OBJ;
    private GameObject RenderCam_of_OBJ;
    private Camera RenderCam;

    private float distance_camerawithobject;
    private string objectname;
    private ObjectTransform control_object;
    private Vector3 original_offset;
    private Vector3 old_localscale;
    private Vector3 new_localscale;
    private int _time = 0;

    private void Start()
    {
        OBJ_True = GameObject.Find("Object1").transform.GetChild(0).gameObject;
        Box_of_OBJ = GameObject.Find("Object1").transform.GetChild(1).gameObject;
        Cam_of_OBJ = transform.GetChild(0).gameObject;
        RenderCam_of_OBJ = Cam_of_OBJ.transform.GetChild(0).gameObject;
        RenderCam = RenderCam_of_OBJ.GetComponent<Camera>();
        distance_camerawithobject = Vector3.Distance(new Vector3(0, 0, 0), Cam_of_OBJ.transform.position);
        objectname = transform.name;
        original_offset = OBJ_True.transform.eulerAngles;

    }

    void Update()
    {

        if (ControlObjectPosition.Object_control_center.TryGetValue(objectname, out _))
        {
            control_object = ControlObjectPosition.Object_control_center[objectname];
            Vector3 Obj_position = new Vector3(control_object.PosX, control_object.PosY, control_object.PosZ);
            Quaternion Obj_rotation = new Quaternion(control_object.RotX, control_object.RotY, control_object.RotZ, control_object.RotW);

            transform.position = Obj_position;
            OBJ_True.transform.rotation = Obj_rotation;
            OBJ_True.transform.eulerAngles += original_offset;

            if (_time == 0)
            {
                old_localscale = new Vector3(control_object.ScaleX, control_object.ScaleY, control_object.ScaleZ);
                _time += 1;
            }
            else
            {
                new_localscale = new Vector3(control_object.ScaleX, control_object.ScaleY, control_object.ScaleZ);
                if (new_localscale != old_localscale)
                {

                    float Scaleratio = new_localscale.x / old_localscale.x;
                    OBJ_True.transform.localScale = OBJ_True.transform.localScale * Scaleratio;
                    Box_of_OBJ.transform.localScale = Box_of_OBJ.transform.localScale * Scaleratio;
                    Cam_of_OBJ.transform.position = new Vector3(Cam_of_OBJ.transform.position.x, Cam_of_OBJ.transform.position.y, Cam_of_OBJ.transform.position.z * Scaleratio);
                    distance_camerawithobject = distance_camerawithobject * Scaleratio;
                    RenderCam_of_OBJ.transform.position = new Vector3(RenderCam_of_OBJ.transform.position.x, RenderCam_of_OBJ.transform.position.y * Scaleratio, RenderCam_of_OBJ.transform.position.z);
                    RenderCam.orthographicSize = RenderCam.orthographicSize * Scaleratio;
                }
                old_localscale = new_localscale;
            }
        }
        else
        {
            Debug.Log("No find value");
        }

        Cam_of_OBJ.transform.position = GetCameraposition(User_position, transform); //�p��۾����\�]��m
        Cam_of_OBJ.transform.rotation = Quaternion.LookRotation(transform.position - Cam_of_OBJ.transform.position, Vector3.up);//�p��۾��[�ݪ�����
        Debug.DrawLine(User_position.position, Cam_of_OBJ.transform.position, Color.blue, 2.5f);
        Debug.DrawLine(Cam_of_OBJ.transform.position, OBJ_True.transform.position, Color.red, 2.5f);

    }


    private Vector3 GetCameraposition(Transform user, Transform lookobject)
    {
        float user_object = Vector3.Distance(user.position, lookobject.position);
        float ratio = distance_camerawithobject / user_object;

        if (ratio < 1)
        {
            ratio = 1;
        }

        Vector3 camera_position = lookobject.position + (user.position - lookobject.position) * ratio;
        return camera_position;
    }

    public static Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint)
    {
        Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);

        float dot = Vector3.Dot(Vector3.forward, forwardVector);

        if (Math.Abs(dot - (-1.0f)) < 0.000001f)
        {
            return new Quaternion(Vector3.up.x, Vector3.up.y, Vector3.up.z, 3.1415926535897932f);
        }
        if (Math.Abs(dot - (1.0f)) < 0.000001f)
        {
            return Quaternion.identity;
        }

        float rotAngle = (float)Math.Acos(dot);
        Vector3 rotAxis = Vector3.Cross(Vector3.forward, forwardVector);
        rotAxis = Vector3.Normalize(rotAxis);
        return CreateFromAxisAngle(rotAxis, rotAngle);
    }

    // just in case you need that function also
    public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
    {
        float halfAngle = angle * .5f;
        float s = (float)Math.Sin(halfAngle);
        Quaternion q;
        q.x = axis.x * s;
        q.y = axis.y * s;
        q.z = axis.z * s;
        q.w = (float)Math.Cos(halfAngle);
        return q;
    }
}
