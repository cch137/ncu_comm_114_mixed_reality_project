using UnityEngine;

public class PlayerSync : MonoBehaviour
{
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    // 控制發送頻率，避免每秒 60 次塞爆 Server
    public float syncRate = 0.1f; // 每 0.1 秒發送一次
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= syncRate)
        {
            SendHeadPose();
            SendHandPose();
            timer = 0;
        }
    }

    void SendHeadPose()
    {
        HeadPoseData myHeadData = new HeadPoseData
        {
            pos = new float[] { head.position.x, head.position.y, head.position.z },
            rot = new float[] { head.rotation.x, head.rotation.y, head.rotation.z, head.rotation.w }
        };
        NetworkManager.Instance.Send<HeadPoseData>("HeadPose", myHeadData);
    }

    void SendHandPose()
    {
        HandPoseData MyHandData = new HandPoseData
        {
            lpos = new float[] { leftHand.position.x, leftHand.position.y, leftHand.position.z },
            lrot = new float[] { leftHand.rotation.x, leftHand.rotation.y, leftHand.rotation.z, leftHand.rotation.w },
            rpos = new float[] { rightHand.position.x, rightHand.position.y, rightHand.position.z },
            rrot = new float[] { rightHand.rotation.x, rightHand.rotation.y, rightHand.rotation.z, rightHand.rotation.w }
        };
        NetworkManager.Instance.Send("HandPose", MyHandData);
    }
}