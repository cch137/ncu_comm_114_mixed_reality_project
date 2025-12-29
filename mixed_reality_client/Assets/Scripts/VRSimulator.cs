using UnityEngine;
using UnityEngine.InputSystem; // 1. 引用新版 Namespace

public class VRSimulator : MonoBehaviour
{
    [Header("綁定你的 VR 物件")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("移動設定")]
    public float moveSpeed = 3.0f;

    // 新版 Input System 的 Mouse Delta 數值通常比較大 (Pixel based)，所以靈敏度要設小一點
    public float lookSpeed = 0.1f;
    public float grabDistance = 5.0f;

    // 讓手保持在鏡頭前方的固定位置
    public Vector3 leftHandOffset = new Vector3(-0.3f, -0.2f, 0.5f);
    public Vector3 rightHandOffset = new Vector3(0.3f, -0.2f, 0.5f);

    private float rotationX = 0;
    private float rotationY = 0;

    // 記錄目前抓到的物件
    private ObjGrabbable grabbedObjLeft;
    private ObjGrabbable grabbedObjRight;

    void Start()
    {
        // 鎖定滑鼠游標，方便像 FPS 一樣操作
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (head == null && Camera.main != null) head = Camera.main.transform;
    }

    void Update()
    {
        // 防呆：確認鍵盤滑鼠存在
        if (Keyboard.current == null || Mouse.current == null) return;

        // 按下 ESC 解鎖滑鼠
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        HandleHeadMovement();
        HandleHandFollow();
        HandleInteraction();
    }

    // 1. 模擬頭盔移動 (FPS 模式) - 改用 Input System
    void HandleHeadMovement()
    {
        if (head == null) return;

        // --- 滑鼠旋轉 ---
        // 讀取滑鼠移動量 (Delta)
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        rotationX += -mouseDelta.y * lookSpeed;
        rotationY += mouseDelta.x * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90, 90);

        head.localRotation = Quaternion.Euler(rotationX, rotationY, 0);

        // --- 鍵盤移動 (WASD) ---
        Vector3 moveDir = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) moveDir.z += 1;
        if (Keyboard.current.sKey.isPressed) moveDir.z -= 1;
        if (Keyboard.current.aKey.isPressed) moveDir.x -= 1;
        if (Keyboard.current.dKey.isPressed) moveDir.x += 1;

        // Q/E 上下移動 (模擬蹲下站起)
        if (Keyboard.current.qKey.isPressed) moveDir.y -= 1;
        if (Keyboard.current.eKey.isPressed) moveDir.y += 1;

        // 施加移動
        head.Translate(moveDir.normalized * moveSpeed * Time.deltaTime);
    }

    // 2. 模擬手部位置 (邏輯不變)
    void HandleHandFollow()
    {
        if (head == null) return;

        if (leftHand != null)
        {
            leftHand.position = head.TransformPoint(leftHandOffset);
            leftHand.rotation = head.rotation;
        }

        if (rightHand != null)
        {
            rightHand.position = head.TransformPoint(rightHandOffset);
            rightHand.rotation = head.rotation;
        }
    }

    // 3. 模擬抓取 - 改用 Input System
    void HandleInteraction()
    {
        // 左鍵 = 左手抓/放
        if (Mouse.current.leftButton.wasPressedThisFrame) TryGrab(leftHand, ref grabbedObjLeft);
        if (Mouse.current.leftButton.wasReleasedThisFrame) TryRelease(ref grabbedObjLeft);

        // 右鍵 = 右手抓/放
        if (Mouse.current.rightButton.wasPressedThisFrame) TryGrab(rightHand, ref grabbedObjRight);
        if (Mouse.current.rightButton.wasReleasedThisFrame) TryRelease(ref grabbedObjRight);
    }

    void TryGrab(Transform hand, ref ObjGrabbable currentGrabbed)
    {
        if (currentGrabbed != null) return;

        Ray ray = new Ray(head.position, head.forward);
        // 這裡 LayerMask 設為 Default，如果你有特定 Layer 可自行修改
        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance))
        {
            ObjGrabbable grabbable = hit.collider.GetComponentInParent<ObjGrabbable>();

            if (grabbable != null)
            {
                currentGrabbed = grabbable;
                currentGrabbed.OnGrabStart();

                // 視覺上黏在手上
                currentGrabbed.transform.SetParent(hand);
                currentGrabbed.transform.localPosition = Vector3.zero;

                Debug.Log($"[模擬器] 抓起了 {grabbable.name}");
            }
        }
    }

    void TryRelease(ref ObjGrabbable currentGrabbed)
    {
        if (currentGrabbed != null)
        {
            currentGrabbed.OnGrabEnd();
            currentGrabbed.transform.SetParent(null);

            Debug.Log($"[模擬器] 放開了 {currentGrabbed.name}");
            currentGrabbed = null;
        }
    }
}