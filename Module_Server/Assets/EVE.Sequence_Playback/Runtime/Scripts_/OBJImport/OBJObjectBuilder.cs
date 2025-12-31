/*
 * Copyright (c) 2019 Dummiesman
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
*/

using Dummiesman;
using EVE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Dummiesman
{
    public class OBJObjectBuilder
    {
        //
        public int PushedFaceCount { get; private set; } = 0;

        //stuff passed in by ctor
        private OBJLoader _loader;
        private string _name;

        private Dictionary<ObjLoopHash, int> _globalIndexRemap = new Dictionary<ObjLoopHash, int>();
        private Dictionary<string, List<int>> _materialIndices = new Dictionary<string, List<int>>();
        private List<int> _currentIndexList;
        private string _lastMaterial = null;

        //our local vert/normal/uv
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<Vector3> _normals = new List<Vector3>();
        private List<Vector2> _uvs = new List<Vector2>();
        public string mtlPath = "";

        //this will be set if the model has no normals or missing normal info
        private bool recalculateNormals = false;

        /// <summary>
        /// Loop hasher helper class
        /// </summary>
        private class ObjLoopHash
        {
            public int vertexIndex;
            public int normalIndex;
            public int uvIndex;

            public override bool Equals(object obj)
            {
                if (!(obj is ObjLoopHash))
                    return false;

                var hash = obj as ObjLoopHash;
                return (hash.vertexIndex == vertexIndex) && (hash.uvIndex == uvIndex) && (hash.normalIndex == normalIndex);
            }

            public override int GetHashCode()
            {
                int hc = 3;
                hc = unchecked(hc * 314159 + vertexIndex);
                hc = unchecked(hc * 314159 + normalIndex);
                hc = unchecked(hc * 314159 + uvIndex);
                return hc;
            }
        }

        public void PreBuild(string pos, string col)
        {
            File.WriteAllBytes(pos, _loader.Vertices.ToByteArray());
            File.WriteAllBytes(col, _loader.Colors.ToColorByteArray());
        }
        public void PreBuild(string faces, string pos, string col, string norm, string uvs, string mtl)
        {
            File.WriteAllBytes(faces, _materialIndices.ToByteArray());
            File.WriteAllBytes(pos, _vertices.ToByteArray());
            File.WriteAllBytes(col, _colors.ToByteArray());
            File.WriteAllBytes(norm, _normals.ToByteArray());
            File.WriteAllBytes(uvs, _uvs.ToByteArray());

            var mtl_origin = Path.Combine(_loader._objInfo.Directory.FullName, mtlPath);
            File.Copy(mtl_origin, Path.Combine(Path.GetDirectoryName(faces), Path.GetFileName(mtl_origin)));

            if (_loader.Textures != null)
            {
                foreach (var t in _loader.Textures)
                {
                    t.Compress(true);
                    string path = mtl.Substring(0, mtl.Length - 4) + "_" + t.name + ".dat";
                    mtlPath += ";" + path;
                    File.WriteAllBytes(path, t.GetPixels32().ToByteArray());
                    t.Destroy();
                }
            }
            File.WriteAllBytes(mtl, mtlPath.ToByteArray());
        }
        public GameObject Build(string faces, string pos, string col, string norm, string uvs, string mtl, string path, Sequence sequence)
        {
            GameObject go;
            //if(Application.platform == RuntimePlatform.Android) 
                go = BuildAndroid(faces, pos, col, norm, uvs, mtl, path, sequence);
            //else go = BuildWindows(faces, pos, col, norm, uvs, mtl, path, sequence);
            return go;
        }

        private GameObject BuildAndroid(string faces, string pos, string col, string norm, string uvs, string mtl, string path, Sequence sequence)
        {
            GameObject go;
            Dictionary<string, int[]> _materialIndices = null;
            var t0 = faces.BeginReadFile();
            var t1 = pos.BeginReadFile();
            var t2 = col.BeginReadFile();
            var t3 = norm.BeginReadFile();
            var t4 = uvs.BeginReadFile();

            string[] data = mtl.ReadFile().ToStringFromBytes().Split(';');
            for (int i = 1; i < data.Length; i++) data[i] = Path.Combine(Path.GetDirectoryName(faces), Path.GetFileName(data[i]));
            if (!string.IsNullOrEmpty(data[0]))
            {
                _loader._objInfo = new FileInfo(path);
                _loader.LoadMaterialLibrary(data, sequence, Path.GetDirectoryName(pos));
            }

            go = new GameObject(_name);
            //add meshrenderer
            var mr = go.AddComponent<MeshRenderer>();
            int submesh = 0;

            t0.WaitToFinish(); if (t0.Failed()) return default; _materialIndices = t0.downloadHandler.data.ToFaceDictionary();
            //locate the material for each submesh
            Material[] materialArray = new Material[_materialIndices.Count];
            foreach (var kvp in _materialIndices)
            {
                Material material = null;
                if (_loader.Materials == null)
                {
                    material = OBJLoaderHelper.CreateNullMaterial(sequence);
                    //material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = _loader.Materials.First().Value;// OBJLoaderHelper.CreateNullMaterial();
                                                                   //material.name = kvp.Key;
                        _loader.Materials[kvp.Key] = material;
                    }
                }
                materialArray[submesh] = material;
                submesh++;
            }
            mr.sharedMaterials = materialArray;

            //add meshfilter
            var mf = go.AddComponent<MeshFilter>();
            submesh = 0;

            t1.WaitToFinish(); if (t1.Failed()) return default; _vertices = t1.downloadHandler.data.ToVector3List();
            var msh = new Mesh()
            {
                name = _name,
                indexFormat = (_vertices.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                subMeshCount = _materialIndices.Count
            };

            //set vertex data
            msh.SetVertices(_vertices);

            t2.WaitToFinish(); if (t2.Failed()) return default; _colors = t2.downloadHandler.data.ToColorList();
            msh.SetColors(_colors);

            t3.WaitToFinish(); if (t3.Failed()) return default; _normals = t3.downloadHandler.data.ToVector3List();
            msh.SetNormals(_normals);

            t4.WaitToFinish(); if (t4.Failed()) return default; _uvs = t4.downloadHandler.data.ToVector2List();
            msh.SetUVs(0, _uvs);

            //set faces
            foreach (var kvp in _materialIndices)
            {
                msh.SetTriangles(kvp.Value, submesh);
                submesh++;
            }

            mf.sharedMesh = msh;
            return go;
        }

        private GameObject BuildWindows(string faces, string pos, string col, string norm, string uvs, string mtl, string path, Sequence sequence)
        {
            GameObject go;
            Dictionary<string, int[]> _materialIndices = null;
            Task[] tasks = new Task[5];
            tasks[0] = Task.Run(() => _materialIndices = faces.ReadFile().ToFaceDictionary());
            tasks[1] = Task.Run(() => _vertices = pos.ReadFile().ToVector3List());
            tasks[2] = Task.Run(() => _colors = col.ReadFile().ToColorList());
            tasks[3] = Task.Run(() => _normals = norm.ReadFile().ToVector3List());
            tasks[4] = Task.Run(() => _uvs = uvs.ReadFile().ToVector2List());

            string[] data = mtl.ReadFile().ToStringFromBytes().Split(';');
            if (!string.IsNullOrEmpty(data[0]))
            {
                _loader._objInfo = new FileInfo(path);
                _loader.LoadMaterialLibrary(data, sequence);
            }

            go = new GameObject(_name);
            //add meshrenderer
            var mr = go.AddComponent<MeshRenderer>();
            int submesh = 0;

            tasks[0].WaitToFinish();
            //locate the material for each submesh
            Material[] materialArray = new Material[_materialIndices.Count];
            foreach (var kvp in _materialIndices)
            {
                Material material = null;
                if (_loader.Materials == null)
                {
                    material = OBJLoaderHelper.CreateNullMaterial(sequence);
                    //material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = _loader.Materials.First().Value;// OBJLoaderHelper.CreateNullMaterial();
                                                                   //material.name = kvp.Key;
                        _loader.Materials[kvp.Key] = material;
                    }
                }
                materialArray[submesh] = material;
                submesh++;
            }
            mr.sharedMaterials = materialArray;

            //add meshfilter
            var mf = go.AddComponent<MeshFilter>();
            submesh = 0;

            tasks[1].WaitToFinish();
            var msh = new Mesh()
            {
                name = _name,
                indexFormat = (_vertices.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                subMeshCount = _materialIndices.Count
            };

            //set vertex data
            msh.SetVertices(_vertices);

            tasks[2].WaitToFinish();
            msh.SetColors(_colors);

            tasks[3].WaitToFinish();
            msh.SetNormals(_normals);

            tasks[4].WaitToFinish();
            msh.SetUVs(0, _uvs);

            //set faces
            foreach (var kvp in _materialIndices)
            {
                msh.SetTriangles(kvp.Value, submesh);
                submesh++;
            }

            mf.sharedMesh = msh;
            return go;
        }

        public GameObject Build(Sequence sequence)
        {
            var go = new GameObject(_name);

            //add meshrenderer
            var mr = go.AddComponent<MeshRenderer>();
            int submesh = 0;


            //locate the material for each submesh
            Material[] materialArray = new Material[_materialIndices.Count];
            foreach (var kvp in _materialIndices)
            {
                Material material = null;
                if (_loader.Materials == null)
                {
                    material = OBJLoaderHelper.CreateNullMaterial(sequence);
                    material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = _loader.Materials.First().Value;// OBJLoaderHelper.CreateNullMaterial();
                        material.name = kvp.Key;
                        _loader.Materials[kvp.Key] = material;
                    }
                }
                materialArray[submesh] = material;
                submesh++;
            }
            mr.sharedMaterials = materialArray;

            //add meshfilter
            var mf = go.AddComponent<MeshFilter>();
            submesh = 0;

            var msh = new Mesh()
            {
                name = _name,
                indexFormat = (_vertices.Count > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
                subMeshCount = _materialIndices.Count
            };

            //set vertex data
            msh.SetVertices(_vertices);
            msh.SetColors(_colors);
            msh.SetNormals(_normals);
            msh.SetUVs(0, _uvs);

            //set faces
            foreach (var kvp in _materialIndices)
            {
                msh.SetTriangles(kvp.Value, submesh);
                submesh++;
            }

            //recalculations
            if (recalculateNormals)
                msh.RecalculateNormals();
            msh.RecalculateTangents();
            msh.RecalculateBounds();

            mf.sharedMesh = msh;

            //
            return go;
        }

        public void SetMaterial(string name)
        {
            if (!_materialIndices.TryGetValue(name, out _currentIndexList))
            {
                _currentIndexList = new List<int>();
                _materialIndices[name] = _currentIndexList;
            }
        }


        public void PushFace(string material, List<int> vertexIndices, List<int> normalIndices, List<int> uvIndices)
        {
            //invalid face size?
            if (vertexIndices.Count < 3)
            {
                return;
            }

            //set material
            if (material != _lastMaterial)
            {
                SetMaterial(material);
                _lastMaterial = material;
            }

            //remap
            int[] indexRemap = new int[vertexIndices.Count];
            for (int i = 0; i < vertexIndices.Count; i++)
            {
                int vertexIndex = vertexIndices[i];
                int normalIndex = normalIndices[i];
                int uvIndex = uvIndices[i];

                var hashObj = new ObjLoopHash()
                {
                    vertexIndex = vertexIndex,
                    normalIndex = normalIndex,
                    uvIndex = uvIndex
                };
                int remap = -1;

                if (!_globalIndexRemap.TryGetValue(hashObj, out remap))
                {
                    //add to dict
                    _globalIndexRemap.Add(hashObj, _vertices.Count);
                    remap = _vertices.Count;

                    //add new verts and what not
                    _vertices.Add((vertexIndex >= 0 && vertexIndex < _loader.Vertices.Count) ? _loader.Vertices[vertexIndex] : Vector3.zero);
                    _colors.Add((vertexIndex >= 0 && vertexIndex < _loader.Vertices.Count) ? new Color(_loader.Colors[vertexIndex].x, _loader.Colors[vertexIndex].y, _loader.Colors[vertexIndex].z) : Color.white);
                    _normals.Add((normalIndex >= 0 && normalIndex < _loader.Normals.Count) ? _loader.Normals[normalIndex] : Vector3.zero);
                    _uvs.Add((uvIndex >= 0 && uvIndex < _loader.UVs.Count) ? _loader.UVs[uvIndex] : Vector2.zero);

                    //mark recalc flag
                    if (normalIndex < 0)
                        recalculateNormals = true;
                }

                indexRemap[i] = remap;
            }


            //add face to our mesh list
            if (indexRemap.Length == 3)
            {
                _currentIndexList.AddRange(new int[] { indexRemap[2], indexRemap[1], indexRemap[0] });
            }
            else if (indexRemap.Length == 4)
            {
                _currentIndexList.AddRange(new int[] { indexRemap[0], indexRemap[1], indexRemap[2] });
                _currentIndexList.AddRange(new int[] { indexRemap[2], indexRemap[3], indexRemap[0] });
            }
            else if (indexRemap.Length > 4)
            {
                for (int i = indexRemap.Length - 1; i >= 2; i--)
                {
                    _currentIndexList.AddRange(new int[] { indexRemap[0], indexRemap[i - 1], indexRemap[i] });
                }
            }

            PushedFaceCount++;
        }

        public OBJObjectBuilder(string name, OBJLoader loader)
        {
            _name = name;
            _loader = loader;
        }
    }
}