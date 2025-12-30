using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LinearMovement : MonoBehaviour
{
    [SerializeField] private Vector3 _direction;
    public Vector3 direction { get { return _direction; } set { _direction = value; } }

    [SerializeField] private float _speed;
    public float speed { get { return _speed; } set { _speed = value; } }

    [SerializeField] private float _acceleration;
    public float acceleration { get { return _acceleration; } set { _acceleration = value; } }

    [SerializeField] private float _deceleration;
    public float deceleration { get { return _deceleration; } set { _deceleration = value; } }

    // ��������ɭ���
    [SerializeField] private Vector3 _minBoundary; // ������̤p���
    public Vector3 minBoundary { get { return _minBoundary; } set { _minBoundary = value; } }

    [SerializeField] private Vector3 _maxBoundary; // ������̤j���
    public Vector3 maxBoundary { get { return _maxBoundary; } set { _maxBoundary = value; } }

    void Update()
    {
        speed += acceleration * Time.deltaTime;
        //transform.position += direction * speed * Time.deltaTime;
        // ����������ˬd�M�ϼu
        Vector3 newPosition = transform.position + direction * speed * Time.deltaTime; // �w�p���U�@�Ӧ�m

        //// �ˬd�O�_�W�X���������
        if (newPosition.x < _minBoundary.x || newPosition.x > _maxBoundary.x)
        {
            direction = new Vector3(-direction.x, direction.y, direction.z); // ���� X ��V
            newPosition.x = Mathf.Clamp(newPosition.x, _minBoundary.x, _maxBoundary.x); // �N��m����b��ɤ�
        }

        if (newPosition.y < _minBoundary.y || newPosition.y > _maxBoundary.y)
        {
            direction = new Vector3(direction.x, -direction.y, direction.z); // ���� Y ��V
            newPosition.y = Mathf.Clamp(newPosition.y, _minBoundary.y, _maxBoundary.y); // �N��m����b��ɤ�
        }

        if (newPosition.z < _minBoundary.z || newPosition.z > _maxBoundary.z)
        {
            direction = new Vector3(direction.x, direction.y, -direction.z); // ���� Z ��V
            newPosition.z = Mathf.Clamp(newPosition.z, _minBoundary.z, _maxBoundary.z); // �N��m����b��ɤ�
        }

        transform.position = newPosition; // ��s�����m
    }

    void OnCollisionEnter(Collision collision)
    {
        speed = deceleration; // �I�����t
        direction = -direction; // �ϦV
        // �I���o�ͮɡA�i�H����@�Ǿާ@�A�Ҧp����ʡB�ϼu��
        Debug.Log("�I����G" + collision.gameObject.name);
    }
}
