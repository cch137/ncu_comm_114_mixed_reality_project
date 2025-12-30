using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleHarmonicMotion : MonoBehaviour
{
    [SerializeField] private float amplitude = 1f; // 振幅
    [SerializeField] private float frequency = 1f; // 頻率
    [SerializeField] private Vector3 direction = Vector3.up; // 振動方向

    private float time = 0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;
        Vector3 offset = direction * amplitude * Mathf.Sin(2 * Mathf.PI * frequency * time);
        transform.position = transform.position + offset;
    }
}
