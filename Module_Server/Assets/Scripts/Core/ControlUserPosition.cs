using Microsoft.MixedReality.WebRTC.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlUserPosition : MonoBehaviour
{
    [SerializeField]
    PeerConnection peerConnection;

    // recived camera data parameter
    private double position_x;
    private double position_y;
    private double position_z;
    private double rotation_x;
    private double rotation_y;
    private double rotation_z;
    //string remoteID;
    //bool name = false;

    void Update()
    {
        string Received_json = peerConnection.GetCameraPosition();
        if (Received_json == "")
        {
            Debug.Log("Not connecting");
        }
        else
        {
            string[] subs = Received_json.Split(',');
            Debug.Log(Received_json);
            position_x = double.Parse(subs[0]);
            position_y = double.Parse(subs[1]);
            position_z = double.Parse(subs[2]);
            rotation_x = double.Parse(subs[3]);
            rotation_y = double.Parse(subs[4]);
            rotation_z = double.Parse(subs[5]);
            Vector3 position_trace = new Vector3((float)position_x, (float)position_y, (float)position_z);
            Vector3 rotation_trace = new Vector3((float)rotation_x, (float)rotation_y, (float)rotation_z);
            transform.SetPositionAndRotation(position_trace, Quaternion.Euler(rotation_trace));
        }
    }
}

