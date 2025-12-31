using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlStreamingCamera : MonoBehaviour
{
    [SerializeField]
    Transform User_position;

    private GameObject OBJ_True;
    private GameObject Cam_of_OBJ;
    private GameObject RenderCam_of_OBJ;
    public Camera objectstreamingCam;
    private float distance_camerawithobject;
    private float old_Scaleratio;
    private float camera_orth_size;
    private double Fov_angle = 60.0;

    private void Start()
    {
        // Safe lookup for referenced objects; provide clear error messages if something is missing.
        GameObject manager = GameObject.Find("ObjectManager");
        if (manager == null)
        {
            Debug.LogError("ControlStreamingCamera.Start: 'ObjectManager' GameObject not found in scene. Ensure it exists before this object.");
            return;
        }

        Transform myEntry = manager.transform.Find(this.gameObject.name);
        if (myEntry == null)
        {
            Debug.LogError($"ControlStreamingCamera.Start: Could not find child named '{this.gameObject.name}' under 'ObjectManager'.");
            return;
        }

        if (myEntry.childCount == 0)
        {
            Debug.LogError($"ControlStreamingCamera.Start: The object '{this.gameObject.name}' under 'ObjectManager' has no children to use as OBJ_True.");
            return;
        }

        OBJ_True = myEntry.GetChild(0).gameObject;

        if (transform.childCount == 0)
        {
            Debug.LogError($"ControlStreamingCamera.Start: '{this.gameObject.name}' has no child camera object (expected at least 1 child).");
            return;
        }

        Cam_of_OBJ = transform.GetChild(0).gameObject;
        if (Cam_of_OBJ.transform.childCount == 0)
        {
            Debug.LogError($"ControlStreamingCamera.Start: Camera object '{Cam_of_OBJ.name}' has no child render camera.");
            return;
        }

        RenderCam_of_OBJ = Cam_of_OBJ.transform.GetChild(0).gameObject;
        objectstreamingCam = RenderCam_of_OBJ.GetComponent<Camera>();

        if (objectstreamingCam == null)
        {
            Debug.LogError($"ControlStreamingCamera.Start: No Camera component found on '{RenderCam_of_OBJ.name}'.");
            return;
        }

        camera_orth_size = objectstreamingCam.orthographicSize;
        ObjectSize objSizeComponent = OBJ_True.GetComponent<ObjectSize>();
        if (objSizeComponent != null)
        {
            float maxSize = objSizeComponent.obj_cam_distance();

            objectstreamingCam.orthographicSize = maxSize / 2f;

            camera_orth_size = objectstreamingCam.orthographicSize;
            distance_camerawithobject = camera_orth_size / (float)Math.Tan(Fov_angle * Math.PI / 180 / 2);
        }
        else
        {
            objectstreamingCam.orthographicSize = camera_orth_size;
            distance_camerawithobject = camera_orth_size / (float)Math.Tan(Fov_angle * Math.PI / 180 / 2);
        }

        distance_camerawithobject = camera_orth_size / (float)Math.Tan(Fov_angle * Math.PI / 180 / 2);

        Debug.Log("dis" + distance_camerawithobject);
    }

    void Update()
    {
        // Guard against missing references at runtime
        if (User_position == null)
        {
            Debug.LogError("ControlStreamingCamera.Update: 'User_position' Transform is not assigned in the Inspector.");
            return;
        }
        if (OBJ_True == null)
        {
            Debug.LogError("ControlStreamingCamera.Update: 'OBJ_True' is null. Ensure Start() completed successfully and references exist.");
            return;
        }
        if (Cam_of_OBJ == null)
        {
            Debug.LogError("ControlStreamingCamera.Update: 'Cam_of_OBJ' is null.");
            return;
        }

        Cam_of_OBJ.transform.position = GetCameraposition(User_position, OBJ_True.transform);
        Cam_of_OBJ.transform.rotation = Quaternion.LookRotation(OBJ_True.transform.position - Cam_of_OBJ.transform.position, Vector3.up);

        Debug.DrawLine(User_position.position, Cam_of_OBJ.transform.position, Color.blue, 2.5f);
        Debug.DrawLine(Cam_of_OBJ.transform.position, OBJ_True.transform.position, Color.red, 2.5f);
    }


    private Vector3 GetCameraposition(Transform user, Transform lookobject)
    {

        bool camerachange = false;

        // Safely get AllUpdateObject from ObjectManager. Avoid calling GetComponent on a null GameObject.
        GameObject managerGO = GameObject.Find("ObjectManager");
        if (managerGO == null)
        {
            Debug.LogWarning("ControlStreamingCamera.GetCameraposition: 'ObjectManager' not found. Skipping camera change checks.");
        }
        else
        {
            AllUpdateObject allUpd = managerGO.GetComponent<AllUpdateObject>();
            if (allUpd == null)
            {
                Debug.LogWarning("ControlStreamingCamera.GetCameraposition: 'AllUpdateObject' component not found on 'ObjectManager'. Skipping camera change checks.");
            }
            else
            {
                camerachange = allUpd.Object_camerachange(this.gameObject.name);
                if (camerachange == true)
                {
                    float Scaleratio = allUpd.Object_ratio(this.gameObject.name);
                    Debug.Log("----------------------------------");
                    Debug.Log($"Scaleratio");
                    Debug.Log(Scaleratio);
                    Debug.Log("----------------------------------");
                    if (Scaleratio != 0 && Scaleratio != 1)
                    {
                        if (Scaleratio != old_Scaleratio)
                        {
                            distance_camerawithobject = distance_camerawithobject * Scaleratio;
                            Debug.Log($"distance_camerawithobject: {distance_camerawithobject}");

                            if (objectstreamingCam != null)
                            {
                                objectstreamingCam.orthographicSize = objectstreamingCam.orthographicSize * Scaleratio;
                                camera_orth_size = objectstreamingCam.orthographicSize;
                            }
                            else
                            {
                                Debug.LogWarning("ControlStreamingCamera.GetCameraposition: objectstreamingCam is null, cannot adjust orthographicSize.");
                            }

                            old_Scaleratio = Scaleratio;
                        }
                    }
                    camerachange = false;
                }
            }
        }

        if (user == null)
        {
            Debug.LogError("ControlStreamingCamera.GetCameraposition: 'user' Transform argument is null.");
            return Vector3.zero;
        }
        if (lookobject == null)
        {
            Debug.LogError("ControlStreamingCamera.GetCameraposition: 'lookobject' Transform argument is null.");
            return Vector3.zero;
        }

        float user_object = Vector3.Distance(user.position, lookobject.position);
        if (Math.Abs(user_object) < 1e-6f)
        {
            Debug.LogWarning("ControlStreamingCamera.GetCameraposition: user and lookobject are at the same position or too close; returning lookobject.position.");
            return lookobject.position;
        }

        float ratio = distance_camerawithobject / user_object;

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
