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

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using Dummiesman;
using Pcx;
using EVE;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Dummiesman
{
    public enum SplitMode
    {
        None,
        Object,
        Material
    }

    public class OBJLoader
    {
        //options
        /// <summary>
        /// Determines how objects will be created
        /// </summary>
        public SplitMode SplitMode = SplitMode.Object;

        //global lists, accessed by objobjectbuilder
        internal List<Vector3> Vertices = new List<Vector3>();
        internal List<Vector3> Colors = new List<Vector3>();
        internal List<Vector3> Normals = new List<Vector3>();
        internal List<Vector2> UVs = new List<Vector2>();

        //materials, accessed by objobjectbuilder
        internal Dictionary<string, Material> Materials;
        internal List<Texture2D> Textures;

        //file info for files loaded from file path, used for GameObject naming and MTL finding
        public FileInfo _objInfo;

        /// <summary>
        /// Helper function to load mtllib statements
        /// </summary>
        /// <param name="mtlLibPath"></param>
        public void LoadMaterialLibrary(string mtlLibPath, Sequence sequence)
        {
            if (_objInfo != null)
            {
                if (File.Exists(Path.Combine(_objInfo.Directory.FullName, mtlLibPath)))
                {

                    (Materials, Textures) = new MTLLoader().Load(Path.Combine(_objInfo.Directory.FullName, mtlLibPath), sequence, null, Textures);
                    return;
                }
            }

            if (File.Exists(mtlLibPath))
            {
                (Materials, Textures) = new MTLLoader().Load(mtlLibPath, sequence);
                return;
            }
        }

        public void LoadMaterialLibrary(string[] data, Sequence sequence)
        {
            if (_objInfo != null)
            {
                if (Application.platform == RuntimePlatform.Android || File.Exists(Path.Combine(_objInfo.Directory.FullName, data[0])))
                {
                    (Materials, Textures) = new MTLLoader().Load(Path.Combine(_objInfo.Directory.FullName, data[0]), sequence, data);
                    return;
                }
            }
        }
        public void LoadMaterialLibrary(string[] data, Sequence sequence, string dir)
        {
            string p = Path.Combine(dir, data[0]);
            if (Application.platform == RuntimePlatform.Android || File.Exists(p))
            {
                (Materials, Textures) = new MTLLoader().Load(p, sequence, data);
                return;
            }
        }

        /// <summary>
        /// Load an OBJ file from a stream. No materials will be loaded, and will instead be supplemented by a blank white material.
        /// </summary>
        /// <param name="input">Input OBJ stream</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(Stream input, Sequence sequence)
        {
            var reader = new StreamReader(input);
            //var reader = new StringReader(inputReader.ReadToEnd());

            Dictionary<string, OBJObjectBuilder> builderDict = new Dictionary<string, OBJObjectBuilder>();
            OBJObjectBuilder currentBuilder = null;
            string currentMaterial = "default";

            //lists for face data
            //prevents excess GC
            List<int> vertexIndices = new List<int>();
            List<int> normalIndices = new List<int>();
            List<int> uvIndices = new List<int>();

            Action<string> setCurrentObjectFunc = (string objectName) =>
            {
                if (!builderDict.TryGetValue(objectName, out currentBuilder))
                {
                    currentBuilder = new OBJObjectBuilder(objectName, this);
                    builderDict[objectName] = currentBuilder;
                }
            };

            setCurrentObjectFunc.Invoke("default");
            var buffer = new CharWordReader(reader, 4 * 1024);

            while (true)
            {
                buffer.SkipWhitespaces();
                if (buffer.endReached == true) break;
                buffer.ReadUntilWhiteSpace();
                if (buffer.Is("#"))
                {
                    buffer.SkipUntilNewLine();
                    continue;
                }
                if (Materials == null && buffer.Is("mtllib"))
                {
                    buffer.SkipWhitespaces();
                    buffer.ReadUntilNewLine();
                    LoadMaterialLibrary(buffer.GetString(), sequence);
                    continue;
                }
                if (buffer.Is("v"))
                {
                    Vector3 v = buffer.ReadVector();
                    v.z *= -1;
                    Vertices.Add(v);
                    Colors.Add(buffer.ReadVector());
                    continue;
                }
                if (buffer.Is("vn"))
                {
                    Vector3 v = buffer.ReadVector();
                    v.z *= -1;
                    Normals.Add(v);
                    continue;
                }
                if (buffer.Is("vt"))
                {
                    UVs.Add(buffer.ReadVector());
                    continue;
                }
                if (buffer.Is("usemtl"))
                {
                    buffer.SkipWhitespaces();
                    buffer.ReadUntilNewLine();
                    string materialName = buffer.GetString();
                    currentMaterial = materialName;

                    if (SplitMode == SplitMode.Material)
                    {
                        setCurrentObjectFunc.Invoke(materialName);
                    }
                    continue;
                }
                if ((buffer.Is("o") || buffer.Is("g")) && SplitMode == SplitMode.Object)
                {
                    buffer.ReadUntilNewLine();
                    string objectName = buffer.GetString(1);
                    setCurrentObjectFunc.Invoke(objectName);
                    continue;
                }
                if (buffer.Is("f"))
                {
                    //loop through indices
                    while (true)
                    {
                        bool newLinePassed;
                        buffer.SkipWhitespaces(out newLinePassed);
                        if (newLinePassed == true)
                        {
                            break;
                        }

                        int vertexIndex = int.MinValue;
                        int normalIndex = int.MinValue;
                        int uvIndex = int.MinValue;

                        vertexIndex = buffer.ReadInt();
                        if (buffer.currentChar == '/')
                        {
                            buffer.MoveNext();
                            if (buffer.currentChar != '/')
                            {
                                uvIndex = buffer.ReadInt();
                            }
                            if (buffer.currentChar == '/')
                            {
                                buffer.MoveNext();
                                normalIndex = buffer.ReadInt();
                            }
                        }

                        //"postprocess" indices
                        if (vertexIndex > int.MinValue)
                        {
                            if (vertexIndex < 0)
                                vertexIndex = Vertices.Count - vertexIndex;
                            vertexIndex--;
                        }
                        if (normalIndex > int.MinValue)
                        {
                            if (normalIndex < 0)
                                normalIndex = Normals.Count - normalIndex;
                            normalIndex--;
                        }
                        if (uvIndex > int.MinValue)
                        {
                            if (uvIndex < 0)
                                uvIndex = UVs.Count - uvIndex;
                            uvIndex--;
                        }

                        //set array values
                        vertexIndices.Add(vertexIndex);
                        normalIndices.Add(normalIndex);
                        uvIndices.Add(uvIndex);
                    }

                    //push to builder
                    currentBuilder.PushFace(currentMaterial, vertexIndices, normalIndices, uvIndices);

                    //clear lists
                    vertexIndices.Clear();
                    normalIndices.Clear();
                    uvIndices.Clear();

                    continue;
                }
                buffer.SkipUntilNewLine();
            }

            GameObject obj = new GameObject(_objInfo != null ? Path.GetFileNameWithoutExtension(_objInfo.Name) : "WavefrontObject");

            foreach (var builder in builderDict)
            {
                if (builder.Value.PushedFaceCount == 0) continue;

                GameObject builtObj = builder.Value.Build(sequence);
                builtObj.transform.SetParent(obj.transform, false);
            }

            return obj;
        }

        /// <summary>
        /// Load an OBJ and MTL file from a stream.
        /// </summary>
        /// <param name="input">Input OBJ stream</param>
        /// /// <param name="mtlInput">Input MTL stream</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(Stream input, Stream mtlInput, Sequence sequence)
        {
            var mtlLoader = new MTLLoader();
            (Materials, Textures) = mtlLoader.Load(mtlInput, sequence, null, Textures);

            return Load(input, sequence);
        }

        /// <summary>
        /// Load an OBJ and MTL file from a file path.
        /// </summary>
        /// <param name="path">Input OBJ path</param>
        /// /// <param name="mtlPath">Input MTL path</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(string path, string mtlPath, Sequence sequence)
        {
            int counter = 10000;
            while (!path.IsFileReady() && counter > 0) counter--;
            _objInfo = new FileInfo(path);
            if (!string.IsNullOrEmpty(mtlPath) && File.Exists(mtlPath))
            {
                var mtlLoader = new MTLLoader();
                (Materials, Textures) = mtlLoader.Load(mtlPath, sequence);

                using (var fs = new FileStream(path, FileMode.Open))
                {
                    return Load(fs, sequence);
                }
            }
            else
            {
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    return Load(fs, sequence);
                }
            }
        }

        /// <summary>
        /// Load an OBJ file from a file path. This function will also attempt to load the MTL defined in the OBJ file.
        /// </summary>
        /// <param name="path">Input OBJ path</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(string path, Sequence sequence)
        {
            GameObject obj = Load(path, null, sequence);
            obj.name = Path.GetFileNameWithoutExtension(path);
            return obj;
        }

        public (GameObject, List<Texture2D>) Load(string path, string faces, string pos, string col, string norm, string uvs, string mtl, Sequence sequence)
        {
            GameObject obj = new GameObject(_objInfo != null ? Path.GetFileNameWithoutExtension(_objInfo.Name) : "WavefrontObject");
            obj.transform.localScale = Vector3.one;
            new OBJObjectBuilder("default", this).Build(faces, pos, col, norm, uvs, mtl, path, sequence).transform.SetParent(obj.transform, false);
            obj.name = Path.GetFileNameWithoutExtension(path);

            return (obj, Textures);
        }

        public void PreLoad(string path, string faces, string pos, string col, string norm, string uvs, string mtl, Sequence sequence)
        {
            _objInfo = new FileInfo(path);
            using (var fs = new FileStream(path, FileMode.Open))
            {
                var reader = new StreamReader(fs);

                OBJObjectBuilder currentBuilder = new OBJObjectBuilder("default", this);

                List<int> vertexIndices = new List<int>();
                List<int> normalIndices = new List<int>();
                List<int> uvIndices = new List<int>();
                var buffer = new CharWordReader(reader, 4 * 1024);

                while (true)
                {
                    buffer.SkipWhitespaces();
                    if (buffer.endReached == true) break;
                    buffer.ReadUntilWhiteSpace();
                    if (buffer.Is("#"))
                    {
                        buffer.SkipUntilNewLine();
                        continue;
                    }
                    if (Materials == null && buffer.Is("mtllib"))
                    {
                        buffer.SkipWhitespaces();
                        buffer.ReadUntilNewLine();
                        currentBuilder.mtlPath = buffer.GetString();
                        LoadMaterialLibrary(currentBuilder.mtlPath, sequence);
                        continue;
                    }
                    if (buffer.Is("v"))
                    {
                        Vector3 v = buffer.ReadVector();
                        v.z *= -1;
                        Vertices.Add(v);
                        Colors.Add(buffer.ReadVector());
                        continue;
                    }
                    if (buffer.Is("vn"))
                    {
                        Vector3 v = buffer.ReadVector();
                        v.z *= -1;
                        Normals.Add(v);
                        continue;
                    }
                    if (buffer.Is("vt"))
                    {
                        UVs.Add(buffer.ReadVector());
                        continue;
                    }
                    if (buffer.Is("f"))
                    {
                        //loop through indices
                        while (true)
                        {
                            bool newLinePassed;
                            buffer.SkipWhitespaces(out newLinePassed);
                            if (newLinePassed == true)
                            {
                                break;
                            }

                            int vertexIndex = int.MinValue;
                            int normalIndex = int.MinValue;
                            int uvIndex = int.MinValue;

                            vertexIndex = buffer.ReadInt();
                            if (buffer.currentChar == '/')
                            {
                                buffer.MoveNext();
                                if (buffer.currentChar != '/')
                                {
                                    uvIndex = buffer.ReadInt();
                                }
                                if (buffer.currentChar == '/')
                                {
                                    buffer.MoveNext();
                                    normalIndex = buffer.ReadInt();
                                }
                            }

                            //"postprocess" indices
                            if (vertexIndex > int.MinValue)
                            {
                                if (vertexIndex < 0)
                                    vertexIndex = Vertices.Count - vertexIndex;
                                vertexIndex--;
                            }
                            if (normalIndex > int.MinValue)
                            {
                                if (normalIndex < 0)
                                    normalIndex = Normals.Count - normalIndex;
                                normalIndex--;
                            }
                            if (uvIndex > int.MinValue)
                            {
                                if (uvIndex < 0)
                                    uvIndex = UVs.Count - uvIndex;
                                uvIndex--;
                            }

                            //set array values
                            vertexIndices.Add(vertexIndex);
                            normalIndices.Add(normalIndex);
                            uvIndices.Add(uvIndex);
                        }

                        //push to builder
                        currentBuilder.PushFace("default", vertexIndices, normalIndices, uvIndices);

                        //clear lists
                        vertexIndices.Clear();
                        normalIndices.Clear();
                        uvIndices.Clear();

                        continue;
                    }
                    buffer.SkipUntilNewLine();
                }

                if (currentBuilder.PushedFaceCount != 0) currentBuilder.PreBuild(faces, pos, col, norm, uvs, mtl);
                else currentBuilder.PreBuild(pos, col);
            }
        }
    }
}