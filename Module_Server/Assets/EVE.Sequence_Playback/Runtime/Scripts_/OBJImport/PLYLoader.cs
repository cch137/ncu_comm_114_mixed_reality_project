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
using System.Linq;
using EVE;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Pcx;

namespace Dummiesman
{
    public class PLYLoader
    {
        //options
        /// <summary>
        /// Determines how objects will be created
        /// </summary>
        public SplitMode SplitMode = SplitMode.Object;

        //global lists, accessed by objobjectbuilder
        internal List<System.Numerics.Vector3> Vertices = new List<System.Numerics.Vector3>();
        internal List<System.Numerics.Vector3> Colors = new List<System.Numerics.Vector3>();
        internal List<System.Numerics.Vector3> Normals = new List<System.Numerics.Vector3>();
        internal List<System.Numerics.Vector2> UVs = new List<System.Numerics.Vector2>();

        //materials, accessed by objobjectbuilder
        internal Dictionary<string, Material> Materials;
        internal List<Texture2D> Textures;

        //file info for files loaded from file path, used for GameObject naming and MTL finding
        private System.IO.FileInfo _plyInfo;

        /// <summary>
        /// Helper function to load mtllib statements
        /// </summary>
        /// <param name="mtlLibPath"></param>
        private void LoadMaterialLibrary(string mtlLibPath, Sequence sequence)
        {
            if (_plyInfo != null)
            {
                if (File.Exists(Path.Combine(_plyInfo.Directory.FullName, mtlLibPath)))
                {
                    (Materials, Textures) = new MTLLoader().Load(Path.Combine(_plyInfo.Directory.FullName, mtlLibPath), sequence);
                    return;
                }
            }

            if (File.Exists(mtlLibPath))
            {
                (Materials, Textures) = new MTLLoader().Load(mtlLibPath, sequence);
                return;
            }
        }

        /// <summary>
        /// Load a PLY file from a stream. No materials will be loaded, and will instead be supplemented by a blank white material.
        /// </summary>
        /// <param name="input">Input OBJ stream</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(Stream input)
        {
            var importer = new Pcx.PlyImporter();
            var header = importer.ReadDataHeader(new StreamReader(input));
            var body = importer.ReadDataBody(header, new BinaryReader(input));

            Vertices = body.vertices.ToNumericVector3List();
            Colors = body.colors.ToVector3();

            Dictionary<string, PLYObjectBuilder> builderDict = new Dictionary<string, PLYObjectBuilder>();
            PLYObjectBuilder currentBuilder = null;
            string currentMaterial = "default";

            //lists for face data
            //prevents excess GC
            int[] vertexIndices = new int[3];

            //helper func
            Action<string> setCurrentObjectFunc = (string objectName) =>
            {
                if (!builderDict.TryGetValue(objectName, out currentBuilder))
                {
                    currentBuilder = new PLYObjectBuilder(objectName, this);
                    builderDict[objectName] = currentBuilder;
                }
            };

            //create default object
            setCurrentObjectFunc.Invoke("default");

            for (int i = 0; i < body.faces.Length / 3; i++)
            {
                for (int o = 0; o < 3; o++)
                {
                    int vertexIndex = int.MinValue;

                    vertexIndex = body.faces[i * 3 + o];

                    //"postprocess" indices
                    if (vertexIndex > int.MinValue)
                    {
                        if (vertexIndex < 0)
                            vertexIndex = Vertices.Count - vertexIndex;
                        //vertexIndex--;
                    }

                    //set array values
                    vertexIndices[o] = vertexIndex;
                }

                //push to builder
                currentBuilder.PushFace(currentMaterial, vertexIndices);
            }

            //finally, put it all together
            GameObject obj = new GameObject(_plyInfo != null ? Path.GetFileNameWithoutExtension(_plyInfo.Name) : "WavefrontObject");
            obj.transform.localScale = new Vector3(1f, 1f, 1f);

            foreach (var builder in builderDict)
            {
                //empty object
                if (builder.Value.PushedFaceCount == 0)
                    continue;

                var builtObj = builder.Value.Build();
                builtObj.transform.SetParent(obj.transform, false);
            }

            return obj;
        }

        /// <summary>
        /// Load a PLY and MTL file from a stream.
        /// </summary>
        /// <param name="input">Input OBJ stream</param>
        /// /// <param name="mtlInput">Input MTL stream</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(Stream input, Stream mtlInput, Sequence sequence)
        {
            var mtlLoader = new MTLLoader();
            (Materials, Textures) = mtlLoader.Load(mtlInput, sequence, null, Textures);

            return Load(input);
        }

        /// <summary>
        /// Load a PLY and MTL file from a file path.
        /// </summary>
        /// <param name="path">Input PLY path</param>
        /// /// <param name="mtlPath">Input MTL path</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(string path, string mtlPath, Sequence sequence)
        {
            int counter = 10000;
            while (!path.IsFileReady() && counter > 0) counter--;
            _plyInfo = new FileInfo(path);
            if (!string.IsNullOrEmpty(mtlPath) && File.Exists(mtlPath))
            {
                var mtlLoader = new MTLLoader();
                (Materials, Textures) = mtlLoader.Load(mtlPath, sequence);

                using (var fs = new FileStream(path, FileMode.Open))
                {
                    return Load(fs);
                }
            }
            else
            {
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    return Load(fs);
                }
            }
        }

        /// <summary>
        /// Load a PLY file from a file path. This function will also attempt to load the MTL defined in the PLY file.
        /// </summary>
        /// <param name="path">Input PLY path</param>
        /// <returns>Returns a GameObject represeting the OBJ file, with each imported object as a child.</returns>
        public GameObject Load(string path, Sequence sequence)
        {
            var ret = Load(path, null, sequence);
            ret.name = Path.GetFileNameWithoutExtension(path);
            return ret;
        }

        public GameObject Load(string path, string faces, string pos, string col, string norm, string uvs)
        {
            GameObject obj = new GameObject(_plyInfo != null ? Path.GetFileNameWithoutExtension(_plyInfo.Name) : "WavefrontObject");
            obj.transform.localScale = new Vector3(1f, 1f, 1f);
            new PLYObjectBuilder("default", this).Build(faces, pos, col, norm, uvs).transform.SetParent(obj.transform, false);
            obj.name = Path.GetFileNameWithoutExtension(path);
            return obj;
        }

        public void PreLoad(string path, string faces, string pos, string col, string norm, string uvs)
        {
            PlyImporter.DataBody body;
            using (var fs = new FileStream(path, FileMode.Open))
            {
                var importer = new PlyImporter();
                var header = importer.ReadDataHeader(new StreamReader(fs));
                body = importer.ReadDataBody(header, new BinaryReader(fs));
            }
            PreLoad(body.vertices.ToNumericVector3List(), body.colors.ToVector3(), body.faces, faces, pos, col, norm, uvs);
        }
        public void PreLoad(FrameData data, string faces, string pos, string col, string norm, string uvs)
        {
            var points = data.points.ToList();
            var cols = data.colors.ToColorVector3List();
            PreLoad(points, cols, data.faces, faces, pos, col, norm, uvs);
        }

        private void PreLoad(List<System.Numerics.Vector3> verts, List<System.Numerics.Vector3> cols, IEnumerable<int> fs, string faces, string pos, string col, string norm, string uvs)
        {
            Vertices = verts;
            Colors = cols;

            PLYObjectBuilder currentBuilder = new PLYObjectBuilder("default", this);

            int[] vertexIndices = new int[3];

            var fsc = fs.Count() / 3;
            for (int i = 0; i < fsc; i++)
            {
                for (int o = 0; o < 3; o++)
                {
                    int vertexIndex = fs.ElementAt(i * 3 + o);
                    if (vertexIndex < 0) vertexIndex = Vertices.Count - vertexIndex;
                    vertexIndices[o] = vertexIndex;
                }

                currentBuilder.PushFace("default", vertexIndices);
            }

            if (currentBuilder.PushedFaceCount != 0) currentBuilder.PreBuild(faces, pos, col, norm, uvs);
        }
    }
}