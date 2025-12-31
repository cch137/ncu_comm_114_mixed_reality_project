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
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;
using System;
using EVE;

namespace Dummiesman
{
    public class PLYObjectBuilder
    {
        //
        public int PushedFaceCount { get; private set; } = 0;

        //stuff passed in by ctor
        private PLYLoader _loader;
        private string _name;

        private Dictionary<PlyLoopHash, int> _globalIndexRemap = new Dictionary<PlyLoopHash, int>();
        private Dictionary<string, List<int>> _materialIndices = new Dictionary<string, List<int>>();
        private List<int> _currentIndexList;
        private string _lastMaterial = null;

        //our local vert/normal/uv
        private List<Vector3> _vertices = new List<Vector3>();
        private List<Color> _colors = new List<Color>();
        private List<Vector3> _normals = new List<Vector3>();
        private List<Vector2> _uvs = new List<Vector2>();

        //this will be set if the model has no normals or missing normal info
        private bool recalculateNormals = false;

        /// <summary>
        /// Loop hasher helper class
        /// </summary>
        private class PlyLoopHash
        {
            public int vertexIndex;

            public override bool Equals(object Ply)
            {
                if (!(Ply is PlyLoopHash))
                    return false;

                var hash = Ply as PlyLoopHash;
                return (hash.vertexIndex == vertexIndex);
            }

            public override int GetHashCode()
            {
                int hc = 3;
                hc = unchecked(hc * 314159 + vertexIndex);
                return hc;
            }
        }

        public void PreBuild(string faces, string pos, string col, string norm, string uvs)
        {
            File.WriteAllBytes(faces, _materialIndices.ToByteArray());
            File.WriteAllBytes(pos, _vertices.ToByteArray());
            File.WriteAllBytes(col, _colors.ToByteArray());
            File.WriteAllBytes(norm, _normals.ToByteArray());
            File.WriteAllBytes(uvs, _uvs.ToByteArray());
        }

        public GameObject Build(string faces, string pos, string col, string norm, string uvs)
        {
            GameObject go = null;
            try
            {
                //if(Application.platform == RuntimePlatform.Android) 
                    go = BuildAndroid(faces, pos, col, norm, uvs);
                //else go = BuildWindows(faces, pos, col, norm, uvs);
            }
            catch (System.Exception e)
            {
                Debug.Log(e);
            }
            //Task.Run(() => System.GC.Collect(0));

            return go;
        }

        private GameObject BuildAndroid(string faces, string pos, string col, string norm, string uvs)
        {
            GameObject go;
            Dictionary<string, int[]> _materialIndices = null;
            Vector3[] _vertices = null, _normals = null;
            Vector2[] _uvs = null;
            Color[] _colors = null;

            var t0 = faces.BeginReadFile();
            var t1 = pos.BeginReadFile();
            var t2 = col.BeginReadFile();
            var t3 = norm.BeginReadFile();
            var t4 = uvs.BeginReadFile();

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
                    material = PLYLoaderHelper.CreateNullMaterial();
                    material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = PLYLoaderHelper.CreateNullMaterial();
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

            t1.WaitToFinish(); if (t1.Failed()) return default; _vertices = t1.downloadHandler.data.ToVector3Array();
            t2.WaitToFinish(); if (t2.Failed()) return default; _colors = t2.downloadHandler.data.ToColorArray();
            t3.WaitToFinish(); if (t3.Failed()) return default; _normals = t3.downloadHandler.data.ToVector3Array();
            t4.WaitToFinish(); if (t4.Failed()) return default; _uvs = t4.downloadHandler.data.ToVector2Array();
            var msh = new Mesh()
            {
                name = _name,
                indexFormat = (_vertices.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
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

            mf.sharedMesh = msh;
            return go;
        }
        private GameObject BuildWindows(string faces, string pos, string col, string norm, string uvs)
        {
            GameObject go;
            Dictionary<string, int[]> _materialIndices = null;
            Vector3[] _vertices = null, _normals = null;
            Vector2[] _uvs = null;
            Color[] _colors = null;

            var tasks = new List<Task>();
            tasks.Add(Task.Run(() => _materialIndices = faces.ReadFile().ToFaceDictionary()));
            tasks.Add(Task.Run(() => _vertices = pos.ReadFile().ToVector3Array()));
            tasks.Add(Task.Run(() => _colors = col.ReadFile().ToColorArray()));
            tasks.Add(Task.Run(() => _normals = norm.ReadFile().ToVector3Array()));
            tasks.Add(Task.Run(() => _uvs = uvs.ReadFile().ToVector2Array()));

            go = new GameObject(_name);

            //add meshrenderer
            var mr = go.AddComponent<MeshRenderer>();
            int submesh = 0;

            while (tasks[0].isRunning()) { }
            tasks.RemoveAt(0);
            //locate the material for each submesh
            Material[] materialArray = new Material[_materialIndices.Count];
            foreach (var kvp in _materialIndices)
            {
                Material material = null;
                if (_loader.Materials == null)
                {
                    material = PLYLoaderHelper.CreateNullMaterial();
                    material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = PLYLoaderHelper.CreateNullMaterial();
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

            tasks.WaitToFinish();
            var msh = new Mesh()
            {
                name = _name,
                indexFormat = (_vertices.Length > 65535) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
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

            mf.sharedMesh = msh;
            return go;
        }

        public GameObject Build()
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
                    material = PLYLoaderHelper.CreateNullMaterial();
                    material.name = kvp.Key;
                }
                else
                {
                    if (!_loader.Materials.TryGetValue(kvp.Key, out material))
                    {
                        material = PLYLoaderHelper.CreateNullMaterial();
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


        public void PushFace(string material, int[] vertexIndices) //5.0378s
        {
            //set material
            if (material != _lastMaterial)
            {
                SetMaterial(material);
                _lastMaterial = material;
            }

            //var t = DateTime.Now;
            //ts += DateTime.Now - t;
            //t = DateTime.Now;
            //remap         5.787s
            int[] indexRemap = new int[vertexIndices.Length];
            for (int i = 0; i < vertexIndices.Length; i++)
            {
                int vertexIndex = vertexIndices[i];

                var hashPly = new PlyLoopHash()
                {
                    vertexIndex = vertexIndex,
                };
                int remap = -1;

                if (!_globalIndexRemap.TryGetValue(hashPly, out remap))
                {
                    //add to dict
                    _globalIndexRemap.Add(hashPly, _vertices.Count);
                    remap = _vertices.Count;

                    //add new verts and what not
                    _vertices.Add((vertexIndex >= 0 && vertexIndex < _loader.Vertices.Count) ? new Vector3(_loader.Vertices[vertexIndex].X, _loader.Vertices[vertexIndex].Y, _loader.Vertices[vertexIndex].Z) : Vector3.zero);
                    _colors.Add((vertexIndex >= 0 && vertexIndex < _loader.Vertices.Count) ? new Color(_loader.Colors[vertexIndex].X, _loader.Colors[vertexIndex].Y, _loader.Colors[vertexIndex].Z) : Color.white);
                    _normals.Add(Vector3.zero);
                    _uvs.Add(Vector2.zero);

                    recalculateNormals = true;
                }

                indexRemap[i] = remap;
            }
            //ts2 += DateTime.Now - t;
            //t = DateTime.Now;


            //add face to our mesh list     1.404s
            if (indexRemap.Length == 3)
            {
                _currentIndexList.AddRange(new int[] { indexRemap[0], indexRemap[1], indexRemap[2] });
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
            //ts3 += DateTime.Now - t;

            PushedFaceCount++;
        }

        public PLYObjectBuilder(string name, PLYLoader loader)
        {
            _name = name;
            _loader = loader;
        }
    }
}