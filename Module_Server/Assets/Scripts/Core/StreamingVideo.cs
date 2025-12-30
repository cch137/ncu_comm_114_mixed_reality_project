using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.Unity;

public class StreamingVideo : MonoBehaviour
{
    [SerializeField]
    SceneVideoSource scenevideosource;
    [SerializeField]
    Camera objectstreamingCam;
    [SerializeField]
    NetworkTrafficMonitor trafficMonitor;
    [SerializeField]
    string streamName = "DefaultStream";

    private void Update()
    {
        scenevideosource.ControlCameraBuffer();

        // 更新流量統計
        if (trafficMonitor != null)
        {
            // 假設每個幀的資料大小約為 100KB (這需要根據實際情況調整)
            long estimatedBytesPerFrame = 100 * 1024;
            trafficMonitor.AddTraffic(gameObject.name, streamName, estimatedBytesPerFrame, 0);

            // 每秒顯示一次統計資訊
            if (Time.frameCount % 60 == 0) // 假設 60 FPS
            {
                Debug.Log(trafficMonitor.GetTrafficStats());
            }
        }
    }

    public double GetSubMilliseconds(TimeSpan startTimer, TimeSpan endTimer)
    {
        TimeSpan startSpan = new TimeSpan(startTimer.Ticks);
        TimeSpan nowSpan = new TimeSpan(endTimer.Ticks);
        TimeSpan subTimer = nowSpan.Subtract(startSpan).Duration();
        return subTimer.TotalMilliseconds;
    }
}
