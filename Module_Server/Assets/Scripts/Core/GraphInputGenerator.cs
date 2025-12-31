using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GraphInputGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct NodeFeature
    {
        public Vector3 position;
        public float speed;
        public Vector3 direction;
    }

    [System.Serializable]
    public struct GraphInput
    {
        public List<GameObject> objects;
        public List<Transform> users;
        public List<NodeFeature> nodeFeatures;
        public float[,] w_oo;
        public float[][] w_uo;

        public GraphInput(int objectCount, int userCount)
        {
            objects = new List<GameObject>();
            users = new List<Transform>();
            nodeFeatures = new List<NodeFeature>();
            w_oo = new float[objectCount, objectCount];
            w_uo = new float[userCount][];
            for (int u = 0; u < userCount; u++)
            {
                w_uo[u] = new float[objectCount];
            }
        }
    }

    public GameObject positionSetObject;
    private PositionSet positionSet;
    public GameObject usersParent;
    private List<Camera> userCameras;
    private float collisionRadius = 0.5f;
    private GraphInput graphInput;
    private int lastChildCount = 0;

    void Awake()
    {
        if (positionSetObject != null)
        {
            positionSet = positionSetObject.GetComponent<PositionSet>();
            if (positionSet == null)
            {
                Debug.LogError("PositionSet component not found on the specified GameObject!");
            }
        }
        else
        {
            Debug.LogError("PositionSetObject is not assigned in GraphInputGenerator!");
        }

        if (usersParent == null)
        {
            usersParent = GameObject.Find("Users");
            if (usersParent == null)
            {
                Debug.LogError("Could not find GameObject named 'User' in the scene!");
            }
            else
            {
                Debug.Log($"Found User GameObject: {usersParent.name}");
            }
        }

        userCameras = new List<Camera>();
        FindUserCameras();

        graphInput = new GraphInput(0, userCameras.Count);
        if (userCameras.Count > 0)
        {
            graphInput = GenerateInput();
        }

        lastChildCount = usersParent != null ? usersParent.transform.childCount : 0;
    }

    void Update()
    {
        if (usersParent == null)
        {
            usersParent = GameObject.Find("Users");
            if (usersParent == null)
            {
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log("User GameObject not found. Waiting for it to be created...");
                }
                return;
            }
            else
            {
                Debug.Log($"Found User GameObject: {usersParent.name}");
                lastChildCount = 0;
            }
        }

        int currentChildCount = usersParent.transform.childCount;
        if (currentChildCount != lastChildCount)
        {
            Debug.Log($"Users child count changed from {lastChildCount} to {currentChildCount}.");
            if (currentChildCount > 0)
            {
                string userNames = string.Join(", ", usersParent.transform.Cast<Transform>().Select(t => t.name));
                Debug.Log($"Current users in Users: [{userNames}]");
            }
            else
            {
                Debug.Log("Users is now empty.");
            }
            lastChildCount = currentChildCount;
        }

        FindUserCameras();

        if (userCameras.Count > 0 && Time.frameCount % 60 == 0)
        {
            graphInput = GenerateInput();
            UpdateEdgeRenderers();
        }
        else if (Time.frameCount % 60 == 0)
        {
            Debug.Log("Skipping graph update: No user cameras available.");
        }
    }

    void FindUserCameras()
    {
        userCameras.Clear();
        if (usersParent == null)
        {
            Debug.LogError("Users parent GameObject not assigned!");
            return;
        }

        foreach (Transform user in usersParent.transform)
        {
            Camera[] cameras = user.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name.Contains("User_Position"))
                {
                    userCameras.Add(cam);
                    Debug.Log($"Found camera for user: {user.name} at {cam.gameObject.name}");
                }
            }

            if (cameras.Length == 0)
            {
                Debug.LogWarning($"No cameras found under user: {user.name}");
            }
        }

        if (userCameras.Count == 0)
        {
            Debug.LogWarning($"No user cameras found under {usersParent.name}. Current child count: {usersParent.transform.childCount}");
        }
    }

    public GraphInput GenerateInput()
    {
        GraphInput input = new GraphInput(0, userCameras.Count);

        if (positionSet == null)
        {
            Debug.LogError("PositionSet is not assigned in GraphInputGenerator!");
            return input;
        }

        if (userCameras.Count == 0)
        {
            Debug.LogError("No user cameras found!");
            return input;
        }

        List<GameObject> allModels = new List<GameObject>();
        allModels.AddRange(positionSet.big3dModels);
        allModels.AddRange(positionSet.small3dModels);

        input.w_oo = new float[allModels.Count, allModels.Count];
        input.w_uo = new float[userCameras.Count][];
        for (int u = 0; u < userCameras.Count; u++)
        {
            input.w_uo[u] = new float[allModels.Count];
        }

        foreach (var model in allModels)
        {
            if (model == null) continue;
            SphereCollider collider = model.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = model.AddComponent<SphereCollider>();
                collider.radius = collisionRadius;
                collider.isTrigger = true;
            }
        }

        List<NodeFeature> nodeFeatures = new List<NodeFeature>();

        for (int i = 0; i < allModels.Count; i++)
        {
            if (allModels[i] == null) continue;
            LinearMovement movement = allModels[i].GetComponent<LinearMovement>();
            if (movement == null)
            {
                Debug.LogError($"LinearMovement component missing on {allModels[i].name}");
                continue;
            }

            NodeFeature feature = new NodeFeature
            {
                position = allModels[i].transform.position,
                speed = movement.speed,
                direction = movement.direction.normalized
            };
            nodeFeatures.Add(feature);
        }

        List<Transform> users = new List<Transform>();
        foreach (var camera in userCameras)
        {
            NodeFeature userFeature = new NodeFeature
            {
                position = camera.transform.position,
                speed = 0f,
                direction = camera.transform.forward.normalized
            };
            nodeFeatures.Add(userFeature);
            users.Add(camera.transform);
        }

        for (int i = 0; i < allModels.Count; i++)
        {
            for (int j = 0; j < allModels.Count; j++)
            {
                input.w_oo[i, j] = float.MaxValue;
            }
        }

        for (int i = 0; i < allModels.Count; i++)
        {
            if (allModels[i] == null) continue;
            LinearMovement movement_i = allModels[i].GetComponent<LinearMovement>();
            if (movement_i == null) continue;

            Collider[] hits = Physics.OverlapSphere(allModels[i].transform.position, collisionRadius);
            foreach (var hit in hits)
            {
                GameObject neighbor = hit.gameObject;
                int j = allModels.IndexOf(neighbor);
                if (j == -1 || i == j) continue;

                LinearMovement movement_j = neighbor.GetComponent<LinearMovement>();
                if (movement_j == null) continue;

                if (movement_i.speed >= 1f || movement_j.speed >= 1f)
                {
                    input.w_oo[i, j] = float.MaxValue;
                    continue;
                }

                float dist = Vector3.Distance(allModels[i].transform.position, allModels[j].transform.position);
                float speedDiff = Mathf.Abs(movement_i.speed - movement_j.speed);
                float dirSim = Vector3.Dot(movement_i.direction.normalized, movement_j.direction.normalized);
                input.w_oo[i, j] = dist * (1 + speedDiff) * (1 - dirSim);
            }
        }

        for (int u = 0; u < userCameras.Count; u++)
        {
            Vector3 userDir = userCameras[u].transform.forward.normalized;
            for (int i = 0; i < allModels.Count; i++)
            {
                if (allModels[i] == null)
                {
                    input.w_uo[u][i] = float.MaxValue;
                    continue;
                }

                LinearMovement movement_i = allModels[i].GetComponent<LinearMovement>();
                if (movement_i == null)
                {
                    input.w_uo[u][i] = float.MaxValue;
                    continue;
                }

                float dist = Vector3.Distance(userCameras[u].transform.position, allModels[i].transform.position);
                float speed = movement_i.speed;
                Vector3 userToObject = (allModels[i].transform.position - userCameras[u].transform.position).normalized;
                float cosThetaUO = Vector3.Dot(userDir, userToObject);
                float dirSim = Vector3.Dot(userDir, movement_i.direction.normalized);
                input.w_uo[u][i] = dist * (1 + speed) * (1 - cosThetaUO) * (1 - dirSim);
            }
        }

        input.objects = allModels;
        input.users = users;
        input.nodeFeatures = nodeFeatures;
        return input;
    }

    public void SaveGraphInputToCSV(string filePath)
    {
        GraphInput input = GenerateInput();
        List<GameObject> allModels = input.objects;
        List<NodeFeature> nodeFeatures = input.nodeFeatures;
        float[,] w_oo = input.w_oo;
        float[][] w_uo = input.w_uo;

        using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filePath))
        {
            sw.WriteLine("type,x,y,z,speed,dx,dy,dz");
            for (int i = 0; i < allModels.Count; i++)
            {
                var f = nodeFeatures[i];
                sw.WriteLine($"object,{f.position.x},{f.position.y},{f.position.z},{f.speed},{f.direction.x},{f.direction.y},{f.direction.z}");
            }
            for (int u = 0; u < userCameras.Count; u++)
            {
                var f = nodeFeatures[allModels.Count + u];
                sw.WriteLine($"user{u},{f.position.x},{f.position.y},{f.position.z},{f.direction.x},{f.direction.y},{f.direction.z}");
            }
        }

        using (System.IO.StreamWriter sw = new System.IO.StreamWriter("w_oo.csv"))
        {
            for (int i = 0; i < allModels.Count; i++)
            {
                for (int j = 0; j < allModels.Count; j++)
                {
                    if (j > 0) sw.Write(",");
                    sw.Write(w_oo[i, j]);
                }
                sw.WriteLine();
            }
        }

        for (int u = 0; u < userCameras.Count; u++)
        {
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter($"w_uo_user{u}.csv"))
            {
                for (int i = 0; i < allModels.Count; i++)
                {
                    if (i > 0) sw.Write(",");
                    sw.Write(w_uo[u][i]);
                }
                sw.WriteLine();
            }
        }
    }

    void OnDrawGizmos()
    {
        if (graphInput.objects == null || graphInput.nodeFeatures == null) return;

        List<GameObject> allModels = graphInput.objects;
        List<NodeFeature> nodeFeatures = graphInput.nodeFeatures;
        float[,] w_oo = graphInput.w_oo;
        float[][] w_uo = graphInput.w_uo;
        List<Transform> users = graphInput.users;

        for (int i = 0; i < allModels.Count; i++) // �ץ��G�N u �אּ i
        {
            if (allModels[i] == null) continue;
            Gizmos.color = Color.green;
            float radius = Mathf.Max(0.2f, nodeFeatures[i].speed * 0.5f);
            Gizmos.DrawWireSphere(nodeFeatures[i].position, radius);
        }

        for (int u = 0; u < users.Count; u++)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(nodeFeatures[allModels.Count + u].position, 0.3f);
        }

        for (int i = 0; i < allModels.Count; i++)
        {
            if (allModels[i] == null) continue;
            for (int j = i + 1; j < allModels.Count; j++)
            {
                if (allModels[j] == null) continue;
                if (w_oo[i, j] < float.MaxValue)
                {
                    Vector3 pos_i = nodeFeatures[i].position;
                    Vector3 pos_j = nodeFeatures[j].position;
                    float weight = w_oo[i, j];
                    float alpha = Mathf.Clamp01(1f - weight / 10f);
                    Gizmos.color = new Color(1f, 0f, 0f, alpha);
                    Gizmos.DrawLine(pos_i, pos_j);

#if UNITY_EDITOR
                    Vector3 labelPos = (pos_i + pos_j) / 2f;
                    Handles.Label(labelPos, $"w={weight:F2}");
#endif
                }
            }
        }

        for (int u = 0; u < users.Count; u++)
        {
            Vector3 userPos = nodeFeatures[allModels.Count + u].position;
            for (int i = 0; i < allModels.Count; i++)
            {
                if (allModels[i] == null) continue;
                if (w_uo[u][i] < float.MaxValue)
                {
                    Vector3 objPos = nodeFeatures[i].position;
                    float weight = w_uo[u][i];
                    float alpha = Mathf.Clamp01(1f - weight / 10f);
                    Gizmos.color = new Color(1f, 0f, 1f, alpha);
                    Gizmos.DrawLine(userPos, objPos);

#if UNITY_EDITOR
                    Vector3 labelPos = (userPos + objPos) / 2f;
                    Handles.Label(labelPos, $"w={weight:F2}");
#endif
                }
            }
        }
    }

    private List<GameObject> edgeRenderers = new List<GameObject>();

    void UpdateEdgeRenderers()
    {
        foreach (var renderer in edgeRenderers)
        {
            Destroy(renderer);
        }
        edgeRenderers.Clear();

        if (graphInput.objects == null || graphInput.users == null || graphInput.nodeFeatures == null)
        {
            Debug.LogWarning("GraphInput is not fully initialized. Skipping edge rendering.");
            return;
        }

        List<GameObject> allModels = graphInput.objects;
        List<NodeFeature> nodeFeatures = graphInput.nodeFeatures;
        float[,] w_oo = graphInput.w_oo;
        float[][] w_uo = graphInput.w_uo;
        List<Transform> users = graphInput.users;

        for (int i = 0; i < allModels.Count; i++)
        {
            if (allModels[i] == null) continue;
            for (int j = i + 1; j < allModels.Count; j++)
            {
                if (allModels[j] == null) continue;
                if (w_oo[i, j] < float.MaxValue)
                {
                    Vector3 pos_i = nodeFeatures[i].position;
                    Vector3 pos_j = nodeFeatures[j].position;
                    float weight = w_oo[i, j];
                    float alpha = Mathf.Clamp01(1f - weight / 10f);

                    GameObject edgeObj = new GameObject($"Edge_OO_{i}_{j}");
                    edgeObj.transform.SetParent(transform);
                    LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.SetPosition(0, pos_i);
                    lr.SetPosition(1, pos_j);
                    lr.startWidth = 0.05f;
                    lr.endWidth = 0.05f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = new Color(1f, 0f, 0f, alpha);
                    lr.endColor = new Color(1f, 0f, 0f, alpha);
                    edgeRenderers.Add(edgeObj);
                }
            }
        }

        for (int u = 0; u < users.Count; u++)
        {
            Vector3 userPos = nodeFeatures[allModels.Count + u].position;
            for (int i = 0; i < allModels.Count; i++)
            {
                if (allModels[i] == null) continue;
                if (w_uo[u][i] < float.MaxValue)
                {
                    Vector3 objPos = nodeFeatures[i].position;
                    float weight = w_uo[u][i];
                    float alpha = Mathf.Clamp01(1f - weight / 10f);

                    GameObject edgeObj = new GameObject($"Edge_UO_{u}_{i}");
                    edgeObj.transform.SetParent(transform);
                    LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.SetPosition(0, userPos);
                    lr.SetPosition(1, objPos);
                    lr.startWidth = 0.05f;
                    lr.endWidth = 0.05f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = new Color(1f, 0f, 1f, alpha);
                    lr.endColor = new Color(1f, 0f, 1f, alpha);
                    edgeRenderers.Add(edgeObj);
                }
            }
        }
    }
}