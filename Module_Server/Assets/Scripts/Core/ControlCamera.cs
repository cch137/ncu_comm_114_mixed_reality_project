using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlCamera : MonoBehaviour
{
    private float position_x;
    private float position_y;
    private float position_z;
    private float rotation_x;
    private float rotation_y;
    private float rotation_z;
    private float color_control;
    string remoteID;
    bool name = false;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (name == false)
        {
            remoteID = GameObject.Find("NodeDssSignaler_Send").GetComponent<NodeDssSignaler>().GetRemoteID();
            name = true;
        }
        string Received_json = GameObject.Find(remoteID).transform.GetChild(0).gameObject.GetComponent<PeerConnection>().GetCameraPosition();
        Debug.Log("wwwwwwwwwwwwwwwwww");
        if (Received_json == "")
        {
            Debug.Log("Not connecting");
        }
        else
        {
            //Debug.Log(Received_json);
            //var Current_time = float.Parse(DateTime.Now.ToString("ss.ffffff"));

            string[] subs = Received_json.Split(',');
            position_x = float.Parse(subs[0]);
            position_y = float.Parse(subs[1]);
            position_z = float.Parse(subs[2]);
            rotation_x = float.Parse(subs[3]);
            rotation_y = float.Parse(subs[4]);
            rotation_z = float.Parse(subs[5]);
            //color_control = float.Parse(subs[7]);
            controlcameraposition();
            Debug.Log($"{Received_json}");
        }
    }
    private void controlcameraposition()
    {
        Transform maincam = GameObject.Find(remoteID).transform.GetChild(3).gameObject.transform.GetChild(1).gameObject.GetComponent<Transform>();
        Vector3 position_trace = new Vector3(position_x, position_y, position_z);
        Vector3 rotation_trace = new Vector3(rotation_x, rotation_y, rotation_z);
        maincam.SetPositionAndRotation(position_trace, Quaternion.Euler(rotation_trace));

    }
}
