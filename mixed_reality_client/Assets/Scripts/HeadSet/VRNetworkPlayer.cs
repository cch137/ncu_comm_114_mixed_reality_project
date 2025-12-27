using Unity.Netcode;
using UnityEngine;

public class VRNetworkPlayer : NetworkBehaviour
{
    [Header("對應的 VR 硬體")]
    public Transform headTransform; // 拖曳你的 Avatar 頭部模型
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    // 內部的參考，用來抓取場景中的真實 VR 相機
    private Transform localMainCamera;
    // 假設你有手部追蹤，也要抓取本地的手部控制器
    // private Transform localLeftHand; ...

    public override void OnNetworkSpawn()
    {
        // 只有「擁有這個物件的玩家 (也就是戴頭盔的人)」才執行以下邏輯
        if (IsOwner)
        {
            // 1. 抓取場景中的 Main Camera (代表你的頭盔位置)
            localMainCamera = Camera.main.transform;

            // 2. 為了避免自己擋住自己視線，通常會隱藏自己的頭部模型
            // (這一步看你的遊戲需求，有些人會把 Layer 設為 InvisibleToSelf)
            if (headTransform != null)
                headTransform.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // 如果不是自己的角色，就不要動 (讓 NetworkTransform 去處理別人的位置同步)
        if (!IsOwner) return;

        // 如果是自己的角色，將此網路物件的位置對齊真實頭盔的位置
        if (localMainCamera != null)
        {
            // 同步位置與旋轉
            transform.position = localMainCamera.position;
            transform.rotation = localMainCamera.rotation;

            // 注意：這裡我們「寫入」了 transform，
            // 接下來掛在同一個物件上的 NetworkTransform 組件會偵測到變動，
            // 自動把這個新座標發送給 Server。
        }
    }
}
