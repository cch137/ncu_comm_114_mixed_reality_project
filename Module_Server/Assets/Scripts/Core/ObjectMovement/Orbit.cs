using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Orbit : MonoBehaviour
{
    [SerializeField] private Transform center; // 公轉中心
    [SerializeField] private float radius = 5f; // 公轉半徑
    [SerializeField] private float speed = 2f; // 公轉速度

    private float angle = 0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        angle += speed * Time.deltaTime;
        float x = center.position.x + Mathf.Cos(angle) * radius;
        float z = center.position.z + Mathf.Sin(angle) * radius;
        transform.position = new Vector3(x, transform.position.y, z);
    }
}
