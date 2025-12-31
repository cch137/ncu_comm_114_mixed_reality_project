using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController : MonoBehaviour
{
    [SerializeField] private Vector3 startPosition; // 起始位置
    [SerializeField] private Vector3 endPosition; // 結束位置
    [SerializeField] private float speed; // 移動速度

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.PingPong(Time.time * speed, 1));
        transform.position = Vector3.MoveTowards(transform.position, endPosition, speed * Time.deltaTime);
    }
}
