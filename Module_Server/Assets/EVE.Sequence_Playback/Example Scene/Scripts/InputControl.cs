using UnityEngine;

namespace EVE
{
    public class InputControl : MonoBehaviour
    {
        public GameObject cameraOrbit;
        public Transform target;

        public float rotateSpeed = 8f;
        public bool rotate = false;
        public bool drag = false;

        public float MinZoom = 40000f;
        public float MaxZoom = 1f;
        public float dragSpeed = 0.02f;
        public float dragSpeedFast = 0.04f;
        public bool fast = false;
        private Vector3 dragOrigin, scaleOrigin, dragPositionOrigin;
        public Preset EyeLevel;
        [System.Serializable]
        public class Preset
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        private int clicked;
        private float clickTime;
        private float clickDelay = 0.25f;

        private void Start()
        {
            scaleOrigin = cameraOrbit.transform.localScale;
            EyeLevel.position = target.position;
            EyeLevel.rotation = target.rotation;
            EyeLevel.scale = cameraOrbit.transform.localScale;
        }
        private Vector2 lastAxis = Vector2.zero;
        void LateUpdate()
        {
            if (Input.GetMouseButtonDown(0))
            {
                rotate = true;
                lastAxis = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

                if (clicked == 1 && Time.time - clickTime >= clickDelay) clicked = 0;
                clicked++;
                if (clicked == 1) clickTime = Time.time;
                else
                {
                    if (Time.time - clickTime < clickDelay)
                    {
                        target.SetPositionAndRotation(EyeLevel.position, EyeLevel.rotation);
                        cameraOrbit.transform.localScale = EyeLevel.scale;
                    }
                    clicked = 0;
                }
            }
            if (rotate && Input.GetMouseButtonUp(0)) rotate = false;
            if (rotate)
            {
                Vector3 axis = new Vector3(-(lastAxis.x - Input.mousePosition.x) * 0.1f, -(lastAxis.y - Input.mousePosition.y) * 0.1f, Input.GetAxis("Mouse ScrollWheel"));
                lastAxis = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

                float h = axis.x * rotateSpeed;
                float v = axis.y * rotateSpeed;
                
                target.transform.eulerAngles = new Vector3(target.transform.eulerAngles.x, target.transform.eulerAngles.y + h, target.transform.eulerAngles.z + v);
            }
            float scrollFactor = Input.GetAxis("Mouse ScrollWheel");
            if (scrollFactor != 0)
            {
                if (scrollFactor > 0 && cameraOrbit.transform.localScale.x == MaxZoom)
                {
                    Vector3 t = target.transform.up * scrollFactor * MaxZoom;
                    if (drag) dragPositionOrigin -= t;
                    else target.transform.localPosition -= t;
                }
                else
                {
                    cameraOrbit.transform.localScale = cameraOrbit.transform.localScale * (1f - scrollFactor);
                    if (cameraOrbit.transform.localScale.x > MinZoom)
                        cameraOrbit.transform.localScale = Vector3.one * MinZoom;
                    else if (cameraOrbit.transform.localScale.x < MaxZoom)
                        cameraOrbit.transform.localScale = Vector3.one * MaxZoom;
                }
            }
            if (Input.GetMouseButtonDown(2))
            {
                dragOrigin = Input.mousePosition;
                dragPositionOrigin = target.transform.localPosition;
                drag = true;
            }
            if (drag && Input.GetMouseButtonUp(2)) drag = false;
            if (drag)
            {
                fast = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                Vector3 pos = Input.mousePosition - dragOrigin;

                target.transform.localPosition = dragPositionOrigin + (target.transform.right * -pos.y + target.transform.forward * pos.x) * (fast ? dragSpeedFast : dragSpeed) * Mathf.Sqrt(cameraOrbit.transform.localScale.x / scaleOrigin.x);
            }
        }
    }
}