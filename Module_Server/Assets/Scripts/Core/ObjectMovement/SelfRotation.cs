using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfRotation : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 自轉軸
    [SerializeField] private float rotationSpeed = 30f; // 自轉速度 (角度/秒)

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
