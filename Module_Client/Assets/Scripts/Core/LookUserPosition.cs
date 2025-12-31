using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookUserPosition : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField]
    Transform lookat_camera;

    [SerializeField]
    Transform follow;

    private Vector3 old_localscale;
    private Vector3 new_localscale;

    void Start()
    {
        old_localscale = follow.localScale;
    }

    void Update()
    {
        GetComponent<Transform>().LookAt(lookat_camera);

        transform.position = follow.position;

        new_localscale = follow.localScale;

        if (new_localscale != old_localscale)
        {
            float Scaleratio = new_localscale.x / old_localscale.x;
            transform.localScale = transform.localScale * Scaleratio;
        }
        old_localscale = new_localscale;
    }
}
