using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.WebRTC.Unity;

public class CameraQualityAssigner : MonoBehaviour
{
    [System.Serializable]
    public struct QualityConfig
    {
        public float Qk;
        public int width;
        public int height;
        public string compression;
        public int fps;
        public float bandwidthMbps;
    }

    public GameObject usersRoot;
    public List<QualityConfig> qualityTable = new List<QualityConfig>
    {
        new QualityConfig { Qk = 0.1f, width = 640, height = 480, compression = "High", fps = 15, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.3f, width = 640, height = 480, compression = "Medium", fps = 30, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.4f, width = 1280, height = 720, compression = "High", fps = 15, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.5f, width = 1280, height = 720, compression = "Medium", fps = 30, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.7f, width = 1280, height = 720, compression = "Low", fps = 60, bandwidthMbps = 0f },
        new QualityConfig { Qk = 0.8f, width = 1920, height = 1080, compression = "Medium", fps = 30, bandwidthMbps = 0f },
        new QualityConfig { Qk = 1.0f, width = 1920, height = 1080, compression = "Low", fps = 60, bandwidthMbps = 0f }
    };

    private List<GameObject> clusters = new List<GameObject>();
    private List<Camera> objectCameras = new List<Camera>();
    private List<Microsoft.MixedReality.WebRTC.PeerConnection> peerConnections = new List<Microsoft.MixedReality.WebRTC.PeerConnection>();
    private List<List<VolumetricUpdate>> savedClusterList; // �O�s�ǤJ�� clusterList
    private float[] savedQ_k; // �O�s�ǤJ�� Q_k
    private float[,] savedV_ku; // �O�s�ǤJ�� V_ku
    private bool isResultApplied = false; // �аO�O�_���\���ε��G

    public void ApplyClusteringResults(List<List<VolumetricUpdate>> clusterList, float[] Q_k, float[,] V_ku)
    {
        foreach (var clusterObj in clusters)
        {
            if (clusterObj != null)
            {
                Destroy(clusterObj);
            }
        }
        clusters.Clear();

        (objectCameras, peerConnections) = FindObjectCamerasAndPeerConnections(usersRoot);
        if (objectCameras.Count == 0)
        {
            Debug.LogError("No object cameras found!");
            isResultApplied = false;
            return;
        }

        int K = clusterList.Count;
        if (K > objectCameras.Count)
        {
            Debug.LogWarning($"Cluster count ({K}) exceeds camera count ({objectCameras.Count}). Merging clusters...");
            clusterList = MergeClusters(clusterList, objectCameras.Count);
            K = clusterList.Count;

            float[] new_Q_k = new float[K];
            float[,] new_V_ku = new float[K, V_ku.GetLength(1)];
            for (int k = 0; k < K; k++)
            {
                new_Q_k[k] = Q_k[k];
                for (int u = 0; u < V_ku.GetLength(1); u++)
                {
                    new_V_ku[k, u] = V_ku[k, u];
                }
            }
            Q_k = new_Q_k;
            V_ku = new_V_ku;
        }

        clusters = new List<GameObject>(new GameObject[K]);
        for (int k = 0; k < K; k++)
        {
            GameObject clusterObj = new GameObject($"Cluster_{k}");
            clusters[k] = clusterObj;

            foreach (var obj in clusterList[k])
            {
                obj.OBJ_Pos.transform.SetParent(clusterObj.transform);
            }

            Vector3 clusterCenter = Vector3.zero;
            foreach (var obj in clusterList[k])
            {
                clusterCenter += obj.OBJ_Pos.transform.position;
            }
            clusterCenter /= clusterList[k].Count;

            Camera clusterCamera = objectCameras[k];
            clusterCamera.transform.position = clusterCenter + new Vector3(0, 5, -10);
            clusterCamera.transform.LookAt(clusterCenter);

            ApplyQualitySettings(Q_k[k], clusterObj, clusterCamera);
        }

        // �O�s�ǤJ���ƾ�
        savedClusterList = clusterList;
        savedQ_k = Q_k;
        savedV_ku = V_ku;

        // �ˬd�O�_���\���ε��G
        if (clusterList != null && Q_k != null && V_ku != null)
        {
            Debug.Log($"CameraQualityAssigner received {clusterList.Count} clusters, Q_k length: {Q_k.Length}, V_ku dimensions: {V_ku.GetLength(0)}x{V_ku.GetLength(1)}");
            isResultApplied = true; // �аO���w����
        }
        else
        {
            Debug.LogError("CameraQualityAssigner received invalid clustering results!");
            isResultApplied = false;
        }
    }

    // �ˬd�O�_���\���ε��G
    public bool IsResultApplied()
    {
        return isResultApplied;
    }

    private List<List<VolumetricUpdate>> MergeClusters(List<List<VolumetricUpdate>> clusters, int maxClusters)
    {
        if (clusters.Count <= maxClusters)
            return clusters;

        List<List<VolumetricUpdate>> mergedClusters = new List<List<VolumetricUpdate>>(clusters);
        while (mergedClusters.Count > maxClusters)
        {
            float minDist = float.MaxValue;
            int mergeIndex1 = 0;
            int mergeIndex2 = 1;

            for (int i = 0; i < mergedClusters.Count; i++)
            {
                Vector3 center1 = Vector3.zero;
                foreach (var obj in mergedClusters[i])
                    center1 += obj.OBJ_Pos.transform.position;
                center1 /= mergedClusters[i].Count;

                for (int j = i + 1; j < mergedClusters.Count; j++)
                {
                    Vector3 center2 = Vector3.zero;
                    foreach (var obj in mergedClusters[j])
                        center2 += obj.OBJ_Pos.transform.position;
                    center2 /= mergedClusters[j].Count;

                    float dist = Vector3.Distance(center1, center2);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        mergeIndex1 = i;
                        mergeIndex2 = j;
                    }
                }
            }

            mergedClusters[mergeIndex1].AddRange(mergedClusters[mergeIndex2]);
            mergedClusters.RemoveAt(mergeIndex2);
        }

        return mergedClusters;
    }

    private (List<Camera>, List<Microsoft.MixedReality.WebRTC.PeerConnection>) FindObjectCamerasAndPeerConnections(GameObject usersRoot)
    {
        List<Camera> cameras = new List<Camera>();
        List<Microsoft.MixedReality.WebRTC.PeerConnection> connections = new List<Microsoft.MixedReality.WebRTC.PeerConnection>();

        foreach (Transform user in usersRoot.transform)
        {
            Transform objectCameraTransform = user.Find("Object_Camera");
            if (objectCameraTransform == null)
            {
                Debug.LogWarning($"No Object_Camera found under user {user.name}");
                continue;
            }

            int objectIndex = 1;
            while (true)
            {
                Transform objectCamera = objectCameraTransform.Find($"Render_Object{objectIndex}");
                if (objectCamera == null)
                {
                    break;
                }

                Camera cam = objectCamera.GetComponent<Camera>();
                if (cam == null)
                {
                    Debug.LogWarning($"No Camera component found on {objectCamera.name}");
                    objectIndex++;
                    continue;
                }

                Transform videoSourceTransform = objectCamera.Find($"SceneCaptureVideoSource(Object{objectIndex})");
                if (videoSourceTransform == null)
                {
                    Debug.LogWarning($"No SceneCaptureVideoSource found under {objectCamera.name}");
                    objectIndex++;
                    continue;
                }

                SceneVideoSource videoSource = videoSourceTransform.GetComponent<SceneVideoSource>();
                if (videoSource == null)
                {
                    Debug.LogWarning($"No SceneVideoSource component found on {videoSourceTransform.name}");
                    objectIndex++;
                    continue;
                }

                Microsoft.MixedReality.WebRTC.PeerConnection pc = GetPeerConnectionFromVideoSource(videoSource);
                if (pc != null)
                {
                    cameras.Add(cam);
                    connections.Add(pc);
                }

                objectIndex++;
            }
        }

        Debug.Log($"Found {cameras.Count} object cameras and {connections.Count} PeerConnections");
        return (cameras, connections);
    }

    private Microsoft.MixedReality.WebRTC.PeerConnection GetPeerConnectionFromVideoSource(SceneVideoSource videoSource)
    {
        System.Reflection.FieldInfo mediaLineField = typeof(SceneVideoSource).GetField("_mediaLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (mediaLineField == null)
        {
            Debug.LogError("Could not find _mediaLine field in SceneVideoSource");
            return null;
        }

        MediaLine mediaLine = (MediaLine)mediaLineField.GetValue(videoSource);
        if (mediaLine == null)
        {
            Debug.LogWarning($"No MediaLine found for SceneVideoSource {videoSource.name}");
            return null;
        }

        System.Reflection.FieldInfo peerConnectionField = typeof(MediaLine).GetField("_peerConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (peerConnectionField == null)
        {
            Debug.LogError("Could not find _peerConnection field in MediaLine");
            return null;
        }

        Microsoft.MixedReality.WebRTC.PeerConnection pc = (Microsoft.MixedReality.WebRTC.PeerConnection)peerConnectionField.GetValue(mediaLine);
        if (pc == null)
        {
            Debug.LogWarning($"No PeerConnection found for MediaLine in SceneVideoSource {videoSource.name}");
            return null;
        }

        return pc;
    }

    private void ApplyQualitySettings(float Qk, GameObject cluster, Camera clusterCamera)
    {
        if (Qk <= 0f) return;

        QualityConfig config = qualityTable.Find(c => Mathf.Abs(c.Qk - Qk) < 0.01f);
        if (config.Qk == 0) return;

        if (clusterCamera != null)
        {
            RenderTexture rt = new RenderTexture(config.width, config.height, 24);
            rt.Create();
            clusterCamera.targetTexture = rt;

            SceneVideoSource videoSource = clusterCamera.transform.Find($"SceneCaptureVideoSource(Object{clusterCamera.name.Replace("Render_Object", "")})")?.GetComponent<SceneVideoSource>();
            if (videoSource != null)
            {
                videoSource.ChangeCamera(clusterCamera.gameObject);
                videoSource.ControlCameraBuffer();
            }

            clusterCamera.cullingMask = 0;
            foreach (var renderer in cluster.GetComponentsInChildren<Renderer>())
            {
                int layer = renderer.gameObject.layer;
                clusterCamera.cullingMask |= (1 << layer);
            }
        }

        Application.targetFrameRate = config.fps;

        float qualityLevel = config.compression == "High" ? 0.1f : (config.compression == "Medium" ? 0.5f : 1.0f);
        foreach (Renderer renderer in cluster.GetComponentsInChildren<Renderer>())
        {
            if (renderer.material.HasProperty("_QualityLevel"))
                renderer.material.SetFloat("_QualityLevel", qualityLevel);
        }

        Debug.Log($"Cluster Quality: Qk={Qk}, Resolution={config.width}x{config.height}, FPS={config.fps}, Compression={config.compression}, Bandwidth={config.bandwidthMbps:F2} Mbps");
    }

    void OnDestroy()
    {
        foreach (var clusterObj in clusters)
        {
            if (clusterObj != null)
            {
                Destroy(clusterObj);
            }
        }
        clusters.Clear();
    }
}