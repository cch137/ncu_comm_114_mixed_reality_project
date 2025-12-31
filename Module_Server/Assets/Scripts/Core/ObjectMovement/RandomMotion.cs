using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomMotion : MonoBehaviour
{
    [SerializeField] private float speed = 1f; // 移動速度
    [SerializeField] private float randomness = 0.5f; // 隨機性

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 randomDirection = Random.insideUnitSphere * randomness;
        randomDirection.y = 0f; // 只在水平面上隨機移動
        transform.position += randomDirection * speed * Time.deltaTime;
    }
}
