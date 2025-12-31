using Microsoft.MixedReality.WebRTC.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class ControlUserPosition : MonoBehaviour
{
    [SerializeField]
    PeerConnection peerConnection;

    private string camera_info;
    DataTable trace_raw_data = CreatCSVTable();

    void Update()
    {
        camera_info = GetCameraInfo(transform);
        if (peerConnection != null)
        {
            peerConnection.UseDataChannel("Camera", camera_info);
        }
        else
        {
            Debug.LogWarning("ControlUserPosition: PeerConnection is null - cannot send data channel 'Camera'. Assign a PeerConnection in the inspector or add one to the GameObject.");
        }
        AddDatatoDataTable(trace_raw_data, GetCurrentTimestamp(), transform.position.x, transform.position.y, transform.position.z, transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w);
    }

    private void Awake()
    {
        // Try to auto-assign peerConnection if not set in inspector
        if (peerConnection == null)
        {
            peerConnection = GetComponent<PeerConnection>();
        }
        if (peerConnection == null)
        {
            // This is not fatal here but will prevent UseDataChannel from working until assigned
            Debug.LogWarning("ControlUserPosition Awake: No PeerConnection found on this GameObject or assigned in inspector.");
        }
    }

    private string GetCameraInfo(Transform maincam)
    {
        string position_x = maincam.position.x.ToString("f3");
        string position_y = maincam.position.y.ToString("f3");
        string position_z = maincam.position.z.ToString("f3");
        string rotation_yoll = maincam.eulerAngles.x.ToString("f3");
        string rotation_pitch = maincam.eulerAngles.y.ToString("f3");
        string rotation_raw = maincam.eulerAngles.z.ToString("f3");
        string track_data = position_x + ',' + position_y + ',' + position_z + ',' + rotation_yoll + ',' + rotation_pitch + ',' + rotation_raw;
        return track_data;
    }

    private static DataTable CreatCSVTable()
    {
        DataTable dt = new DataTable("move_trace_raw_data");
        dt.Columns.Add("timestamp");
        dt.Columns.Add("x");
        dt.Columns.Add("y");
        dt.Columns.Add("z");
        dt.Columns.Add("qx");
        dt.Columns.Add("qy");
        dt.Columns.Add("qz");
        dt.Columns.Add("qw");

        DataRow dr = dt.NewRow();
        dr["timestamp"] = "timestamp";
        dr["x"] = "x";
        dr["y"] = "y";
        dr["z"] = "z";
        dr["qx"] = "qx";
        dr["qy"] = "qy";
        dr["qz"] = "qz";
        dr["qw"] = "qw";
        dt.Rows.Add(dr);
        return dt;
    }
    private void AddDatatoDataTable(DataTable dt, long timestamp, float x, float y, float z, float qx, float qy, float qz, float qw)
    {
        DataRow dr = dt.NewRow();
        dr["timestamp"] = timestamp;
        dr["x"] = x;
        dr["y"] = y;
        dr["z"] = z;
        dr["qx"] = qx;
        dr["qy"] = qy;
        dr["qz"] = qz;
        dr["qw"] = qw;
        dt.Rows.Add(dr);
    }
    public DataTable GetRawData()
    {
        return trace_raw_data;
    }
    private long GetCurrentTimestamp()
    {
        return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}