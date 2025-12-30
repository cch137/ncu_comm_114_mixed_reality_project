using UnityEngine;
using System.IO;
using Pcx;
using UnityEngine.VFX;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace EVE
{
    [ExecuteAlways]
    [Serializable]
    public class Sequence : Playable
    {
        public Material Material { get { if (_material == null) _material = new Material(Renderer.I._Material); return _material; } }
        public Material _material = null;

        public Material VertexColorMat;
        public MeshFilter mf;
        public MeshRenderer mr;
        public bool isObj;
        public bool isMesh = true;
        public int framePointCount;
        public int f = -1;
        public int preloadId;
        public object preload;
        public string[] files;
        public VisualEffect _vfx;
        public VisualEffect vfx
        {
            get { if (_vfx == null) _vfx = gameObject.AddComponent<VisualEffect>(); return _vfx; }
        }
        public List<GameObject> frames = new List<GameObject>();
        [SerializeField] Texture _colorTexture2D = null;
        [SerializeField] Texture _positionTexture2D = null;
        public string framePath => Path.Combine(Application.streamingAssetsPath, "PreLoaded Data", filePath.GetHashCode().ToString());
        public bool buffer = false;
        public GameObject go;
        public List<Texture2D> textures;
        public VFXTexture VFXTexture;
        public int generatedFrames;
        public string filePath;
        public int hash;

        public IEnumerator LoadSequence(string path, ComputeShader VFXShader, bool buffer)
        {
            filePath = path;
            hash = filePath.GetHashCode();
            name = $"Sequence {hash}";
            files = Directory.GetFiles(path, "*.obj");
            isObj = files.Length > 0;
            if (!isObj)
            {
                files = Directory.GetFiles(path, "*.ply");
                if (files.Length == 0) yield break;
            }
            files = files.OrderBy(x => x.getNumbersAtEnd()).ToArray();
            VFXTexture = new VFXTexture(VFXShader);
            this.buffer = buffer;
            transform.DestroyChildren();
            Stop();
            yield return LoadFrames();
        }
        private IEnumerator LoadFrames()
        {
            int fileId = -1;
            if (buffer && isObj)
            {
                f = 0;
                while (++fileId < files.Length)
                {
                    Load(fileId);
                    yield return null;
                    generatedFrames++;
                }
                yield return null;
                Play();
            }
            else
            {
                framePath.ResetDirectory();

                while (++fileId < files.Length)
                {
                    PreLoad(fileId, files[fileId]);
                    yield return null;
                    generatedFrames++;
                }
                yield return null;

                FinishLoad();
            }
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
        private void Load(int i)
        {
            try
            {
                isMesh = true;

                var loader = new Dummiesman.OBJLoader();
                go = loader.Load(files[i], this);
                textures.AddRange(loader.Textures);

                go.transform.SetParent(transform);
                go.transform.position = Vector3.zero;
                go.transform.localScale = Vector3.one;
                if (files.Length == 1 && i == 0) go.SetActive(true); else go.SetActive(false);
                if (i != 0) go.SetActive(false);
                frames.Add(go);
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        }
        private void PreLoad(int i, string file)
        {
            try
            {
                if (isObj)//obj mesh
                {
                    new Dummiesman.OBJLoader().PreLoad(file, GetPathFaces(i), GetPathPosition(i), GetPathColor(i), GetPathNormals(i), GetPathUVS(i), GetPathMTL(i), this);
                    isMesh = File.Exists(GetPathFaces(i));
                }
                else
                {
                    var data = new PlyImporter().Import(file);
                    isMesh = data.faces.Length > 0;

                    if (isMesh)//ply mesh
                        new Dummiesman.PLYLoader().PreLoad(data, GetPathFaces(i), GetPathPosition(i), GetPathColor(i), GetPathNormals(i), GetPathUVS(i));
                    else//ply pointcloud
                        new PlyImporter().PreLoad(data, GetPathPosition(i), GetPathColor(i));
                    
                }
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        }
        private void FinishLoad()
        {
            try
            {
                if (!isMesh)
                {
                    VFXTexture.LoadFromFiles(GetPathPosition(0), GetPathColor(0));
                    framePointCount = (int)VFXTexture.framePointCount;
                }

                if (isMesh) Renderer.I.RenderUnlitTexture();
                else StartVFX();
                Play();
                LoadFrame(-1, currentFrame);
            }
            catch (Exception e)
            {
                Log.Print(e);
            }
        }
        public void ImportMesh(GameObject mesh)
        {
            GameObject child = Instantiate(mesh, transform);
            DestroyImmediate(mesh);
            child.gameObject.SetActive(false);
            MeshRenderer meshRenderer = child.GetComponentInChildren<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(meshRenderer.sharedMaterial);
        }
        public void StartVFX()
        {
            vfx.pause = false;
            Renderer.I.VFXFilter(0);
            vfx.Play();
        }

        private void ReLoad() => LoadFrame(-1, f);
        private void Start() {
            CheckSequenceTracker();
            ReLoad();
        }

        private void CheckSequenceTracker()
        {
            bool found = false;
            foreach (var s in Renderer.I.sequences)
            {
                if (s.fp == framePath)
                {
                    s.s = this;
                    found = true;
                    break;
                }
            }
            if (!found) Renderer.I.sequences.Add(ScriptableObject.CreateInstance<SequenceTracker>().Init(this, framePath));
        }

        private void OnBecameVisible() => _Update();
        private void _Update()
        {
            if (!isMesh)
            {
                Renderer.I.VFXFilter(Renderer.I.currentVfx);
                vfx.Reinit();

                if (_positionTexture2D == null) Debug.Log("_positionTexture2D == null");
                else if (vfx.HasTexture("Position Map")) vfx.SetTexture("Position Map", _positionTexture2D);

                if (_colorTexture2D == null) Debug.Log("_colorTexture2D == null");
                else if (vfx.HasTexture("Color Map")) vfx.SetTexture("Color Map", _colorTexture2D);

                if (vfx.HasFloat("Size")) vfx.SetFloat("Size", Renderer.I.pointSize);
                if (vfx.HasUInt("PointCount")) vfx.SetUInt("PointCount", (uint)framePointCount);
            }
            else if (buffer && isObj)
            {
                Renderer.I.RenderMaterial(this, f == -1 ? 0 : f);
            }
        }
        private void ResetPointcloud()
        {
            _positionTexture2D = new Texture2D(0, 0, TextureFormat.RGBAHalf, false);
            _positionTexture2D.name = "Position Map";
            _positionTexture2D.filterMode = FilterMode.Point;

            _colorTexture2D = new Texture2D(0, 0, TextureFormat.RGBA32, false);
            _colorTexture2D.name = "Color Map";
            _colorTexture2D.filterMode = FilterMode.Point;

            (_positionTexture2D as Texture2D).Apply(false, true);
            (_colorTexture2D as Texture2D).Apply(false, true);

            _Update();
        }
        private string GetPathColor(int id)
        {
            return framePath + "/" + id + "C.dat";
        }
        private string GetPathPosition(int id)
        {
            return framePath + "/" + id + "P.dat";
        }
        private string GetPathNormals(int id)
        {
            return framePath + "/" + id + "N.dat";
        }
        private string GetPathUVS(int id)
        {
            return framePath + "/" + id + "U.dat";
        }
        private string GetPathFaces(int id)
        {
            return framePath + "/" + id + "F.dat";
        }
        private string GetPathMTL(int id)
        {
            return framePath + "/" + id + "M.dat";
        }
        //Playback methods
        public override void Stop()
        {
            base.Stop();
            if (isMesh && transform.childCount > 0) transform.GetChild(0).gameObject.SetActive(false);
            else ResetPointcloud();
        }
        public override void Play()
        {
            //if (isMesh && transform.childCount > 0) transform.GetChild(0).gameObject.SetActive(true);
            //else 
            _Update();
            base.Play();
        }
        public override void Pause()
        {
            //if (isMesh && transform.childCount > 0) transform.GetChild(0).gameObject.SetActive(true);
            //else 
            _Update();
            base.Pause();
        }
        public override int GetMaxFrames()
        {
            return files.Length;
        }
        public override bool LoadFrame(double lastFrame, double newFrame)
        {
            if (base.LoadFrame(lastFrame, newFrame))
            {
                int id = Mathf.FloorToInt((float)newFrame);
                if (buffer && isObj)
                {
                    if (id >= frames.Count) return false;
                    if (f != -1) frames[f].SetActive(false);
                    f = id;
                    frames[f].SetActive(true);
                    Renderer.I.RenderMaterial(this, f);
                }
                else
                {
                    f = id;
                    if (!isMesh)
                    {
                        _positionTexture2D?.Destroy();
                        _colorTexture2D?.Destroy();

                        VFXTexture.LoadFromFiles(GetPathPosition(id), GetPathColor(id));
                        framePointCount = (int)VFXTexture.framePointCount;
                        _positionTexture2D = VFXTexture.pt;
                        _colorTexture2D = VFXTexture.ct;

                        _Update();
                    }
                    else
                    {
                        foreach (var mf in transform.GetComponentsInChildren<MeshFilter>())
                        {
                            mf.sharedMesh.Destroy();
                        }
                        transform.DestroyChildren();

                        if (isObj)
                        {
                            if (go != null)
                            {//Unload previous go
                                foreach (var m in go.GetComponent<MeshRenderer>().sharedMaterials) m.Destroy();
                                go.GetComponent<MeshFilter>().sharedMesh.Destroy();
                            }
                            if (textures != null) foreach (var t in textures) t.Destroy();
                            (go, textures) = new Dummiesman.OBJLoader().Load(files[id], GetPathFaces(id), GetPathPosition(id), GetPathColor(id), GetPathNormals(id), GetPathUVS(id), GetPathMTL(id), this);
                        }
                        else go = new Dummiesman.PLYLoader().Load(files[id], GetPathFaces(id), GetPathPosition(id), GetPathColor(id), GetPathNormals(id), GetPathUVS(id));
                        go.transform.SetParent(transform);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localScale = new Vector3(1f, 1f, 1f);
                        if (rawImage != null && textures != null && textures.Count > 0) rawImage.texture = textures[0];
                        //if (!isObj || textures == null || textures.Count == 0) 
                        Renderer.I.Render();
                    }
                }

                return true;
            }
            return false;
        }
        public RawImage rawImage;
        public static bool quitting = false;
        private void OnLevelWasLoaded(int level) => quitting = false;
        private void OnApplicationQuit() => quitting = true;
        void OnDestroy()
        {/*
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && Time.frameCount != 0 && Time.renderedFrameCount != 0 && !quitting)
#else
            if(!quitting)
#endif
            */{
                _positionTexture2D?.Destroy();
                _colorTexture2D?.Destroy();
                if (textures != null) foreach (var t in textures) t.Destroy();
                _material?.Destroy();
            }
        }
    }
}