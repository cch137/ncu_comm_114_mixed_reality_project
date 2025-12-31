using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileMotion : MonoBehaviour
{
    [SerializeField] private float initialVelocity = 10f;
    [SerializeField] private float launchAngle = 45f;
    private float gravity = -9.81f;

    private Vector3 initialPosition;
    private float time = 0f;

    // Start is called before the first frame update
    void Start()
    {
        initialPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        time += Time.deltaTime;
        float x = initialPosition.x + initialVelocity * Mathf.Cos(launchAngle * Mathf.Deg2Rad) * time;
        float y = initialPosition.y + initialVelocity * Mathf.Sin(launchAngle * Mathf.Deg2Rad) * time + 0.5f * gravity * time * time;
        transform.position = new Vector3(x, y, transform.position.z);
    }
}
