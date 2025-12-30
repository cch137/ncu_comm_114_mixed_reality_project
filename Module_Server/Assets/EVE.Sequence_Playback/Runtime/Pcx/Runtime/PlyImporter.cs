// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
#if UNITY_EDITOR

#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Threading.Tasks;
using System.Globalization;

namespace Pcx {

    public static class ExtensionMethods
    {
        public static void ParallelFor<T>(this T[] array, Action<int> action, int div = 1)
        {
            int l = array.Length / div;
            int o = Mathf.FloorToInt(Mathf.Sqrt(l));
            Parallel.For(0, o, i =>
            {
                i *= o;
                var end = i + o;
                for (int u = i; u < end; u++) action(u);
            });
            Parallel.For(o * o, l, action);
        }
        public static string XmlToString(this string xml)
        {
            return xml.Replace(">", ">\n");
        }
        public static byte[] ToBytes(this float value)
        {
            return BitConverter.GetBytes(value);
        }
        public static byte[] ToBytes(this string value)
        {
            return System.Text.Encoding.ASCII.GetBytes(value);
        }
        public static bool IsFileReady(this string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool IsBetween(this int value, int left, int right)
        {
            return value >= left && value <= right;
        }
        public static byte[] ToByteArray(this System.Numerics.Vector3[] list)
        {
            var a = new byte[list.Length * 12];
            int i = 0;
            foreach (var v in list)
            {
                foreach (var b in BitConverter.GetBytes(v.X)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.Y)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.Z)) a[i++] = b;
            }
            return a;
        }
    }
    public class FrameData {
        public FrameData(System.Numerics.Vector3[] p, byte[] c, int i, uint f)
        {
            sensorID = 0;
            points = p;
            colors = c;
            id = i;
            firstPointCloudSize = f;
        }
        public FrameData(System.Numerics.Vector3[] p, byte[] c, int[] f, int i, uint fp)
        {
            sensorID = 0;
            points = p;
            colors = c;
            id = i;
            faces = f;
            firstPointCloudSize = fp;
        }
        public FrameData(byte[] c, int i)
        {
            sensorID = 0;
            colors = c;
            id = i;
        }
        public void SaveAsPly(string path)
        {
            BinaryWriter w = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write), System.Text.Encoding.ASCII);
            int count = points == null ? 0 : points.Length;
            int faceCount = faces == null ? 0 : faces.Length / 3;
            w.Write("ply\n".ToBytes());
            w.Write("format binary_little_endian 1.0\n".ToBytes());
            w.Write(("element vertex " + count + "\n").ToBytes());
            w.Write("property float x\n".ToBytes());
            w.Write("property float y\n".ToBytes());
            w.Write("property float z\n".ToBytes());
            w.Write("property uchar red\n".ToBytes());
            w.Write("property uchar green\n".ToBytes());
            w.Write("property uchar blue\n".ToBytes());
            if (faceCount > 0)
            {
                w.Write(("element face " + faceCount + "\n").ToBytes());
                w.Write("property list uchar int vertex_indices\n".ToBytes());
            }
            w.Write("end_header\n".ToBytes());

            for (int i = 0; i < count; i++)
            {
                w.Write(points[i].X.ToBytes());
                w.Write(points[i].Y.ToBytes());
                w.Write(points[i].Z.ToBytes());
                w.Write(colors[i * 3 + 2]);
                w.Write(colors[i * 3 + 1]);
                w.Write(colors[i * 3 + 0]);
            }
            if (faceCount > 0)
            {
                for (int i = 0; i < faceCount; i++)
                {
                    w.Write((byte)3);
                    w.Write(faces[i * 3 + 0]);
                    w.Write(faces[i * 3 + 1]);
                    w.Write(faces[i * 3 + 2]);
                }
            }
            w.Close();
            while (!path.IsFileReady()) { w.Close(); }
        }
        public FrameData()
        {
            sensorID = 0;
        }
        public void Split(bool byLeftSide)
        {
            if (faces != null)
            {
                var f = new List<int>();
                int left = byLeftSide ? 0 : (int)firstPointCloudSize;
                int right = byLeftSide ? (int)firstPointCloudSize : points.Length;
                for (int i = 0; i < faces.Length; i += 3)
                    if (faces[i].IsBetween(left, right) && faces[i + 1].IsBetween(left, right) && faces[i + 2].IsBetween(left, right))
                    {
                        f.Add(faces[i]);
                        f.Add(faces[i + 1]);
                        f.Add(faces[i + 2]);
                    }
                if (!byLeftSide) for (int i = 0; i < f.Count; i++) f[i] -= (int)firstPointCloudSize;
                faces = f.ToArray();
            }
            points = (byLeftSide ? points.Take((int)firstPointCloudSize) : points.Skip((int)firstPointCloudSize)).ToArray();
            colors = (byLeftSide ? colors.Take((int)firstPointCloudSize * 3) : colors.Skip((int)firstPointCloudSize * 3)).ToArray();
        }
        public FrameData Add(FrameData b)
        {
            bool test = (faces != null && faces.Length > 0) || (b.faces != null && b.faces.Length > 0);
            if (test)
            {
                int count = points.Length;
                int toFaces = faces.Length;
                faces = faces.Concat(b.faces).ToArray();
                for (int i = toFaces; i < faces.Length; i++) faces[i] += count;
            }
            points = points.Concat(b.points).ToArray();
            colors = colors.Concat(b.colors).ToArray();
            return this;
        }
        public int[] faces;
        public System.Numerics.Vector3[] points;
        public byte[] colors;
        public int id, sensorID, counter;
        public uint firstPointCloudSize;
    }
#if UNITY_EDITOR
    [UnityEditor.AssetImporters.ScriptedImporter(1, "ply")]
#endif
    public class PlyImporter
#if UNITY_EDITOR
        : UnityEditor.AssetImporters.ScriptedImporter
#endif
    {
        #region ScriptedImporter implementation

        public enum ContainerType { Mesh, ComputeBuffer, Texture }

        [SerializeField] ContainerType _containerType = ContainerType.Texture;

        public FrameData Import(string path, bool invert = true)
        {
            try
            {
                FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                StreamReader streamReader = new StreamReader(stream);
                DataHeader header = ReadDataHeader(streamReader); //1.183 seconds [Could be lower]
                DataBody body = null;
                switch (header.format)
                {
                    case PlyFormat.binary_little_endian:
                        var offset = stream.Position;
                        stream.Close();
                        body = ReadDataBody(header, File.ReadAllBytes(path), offset);
                        break;
                    case PlyFormat.ascii:
                        body = ReadDataBody(header, streamReader);
                        stream.Close();
                        break;
                }
                var data = new FrameData();

                data.points = new System.Numerics.Vector3[body.vertices.Count];
                int z = invert && header.faceCount > 0 ? -1 : 1;
                data.points.ParallelFor(i =>
                {
                    data.points[i].X = body.vertices[i].x;
                    data.points[i].Y = body.vertices[i].y;
                    data.points[i].Z = body.vertices[i].z * z;
                });

                data.colors = new byte[body.colors.Count * 3];
                data.colors.ParallelFor(i => {
                    data.colors[i * 3 + 0] = body.colors[i].b;
                    data.colors[i * 3 + 1] = body.colors[i].g;
                    data.colors[i * 3 + 2] = body.colors[i].r;
                }, 3);
                data.faces = body.faces.ToArray();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        public void PreLoad(FrameData data, string pos, string col)
        {
            File.WriteAllBytes(pos, data.points.ToByteArray());
            File.WriteAllBytes(col, data.colors);
        }

        public GameObject ImportMesh(string path)
        {
            var gameObject = new GameObject();
            var mesh = ImportAsMesh(path);

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetDefaultMaterial();

            gameObject.name = Path.GetFileNameWithoutExtension(path);
            return gameObject;
        }

        public BakedPointCloud ImportTexture(string path)
        {
            return ImportAsBakedPointCloud(path);
        }

#if UNITY_EDITOR
        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext context)
        {
            if (_containerType == ContainerType.Mesh) ImportAsMesh(context);
            else if (_containerType == ContainerType.ComputeBuffer) ImportAsComputeBuffer(context);
            else ImportAsTexture(context);
        }

        public void ImportAsMesh(UnityEditor.AssetImporters.AssetImportContext context)
        {
            // Mesh container
            // Create a prefab with MeshFilter/MeshRenderer.
            var gameObject = new GameObject();
            var mesh = ImportAsMesh(context.assetPath);

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetDefaultMaterial();

            context.AddObjectToAsset("prefab", gameObject);
            if (mesh != null) context.AddObjectToAsset("mesh", mesh);

            context.SetMainObject(gameObject);
        }

        public void ImportAsComputeBuffer(UnityEditor.AssetImporters.AssetImportContext context)
        {
            // ComputeBuffer container
            // Create a prefab with PointCloudRenderer.
            var gameObject = new GameObject();
            var data = ImportAsPointCloudData(context.assetPath);

            var renderer = gameObject.AddComponent<PointCloudRenderer>();
            renderer.sourceData = data;

            context.AddObjectToAsset("prefab", gameObject);
            if (data != null) context.AddObjectToAsset("data", data);

            context.SetMainObject(gameObject);
        }

        public void ImportAsTexture(UnityEditor.AssetImporters.AssetImportContext context)
        {
            var data = ImportAsBakedPointCloud(context.assetPath);
            if (data != null)
            {
                context.AddObjectToAsset("container", data);
                context.AddObjectToAsset("position", data.positionMap);
                context.AddObjectToAsset("color", data.colorMap);
                context.SetMainObject(data);
            }
        }
#endif
        #endregion

        #region Internal utilities

        static Material GetDefaultMaterial()
        {
            return Resources.Load<Material>("Materials/DefaultPoint");
        }

        #endregion

        #region Internal data structure

        public enum DataProperty
        {
            Invalid,
            R8, G8, B8, A8,
            R16, G16, B16, A16,
            SingleX, SingleY, SingleZ,
            NX, NY, NZ,
            DoubleX, DoubleY, DoubleZ,
            Data8, Data16, Data32, Data64,
            LVI
        }
        public enum PlyFormat
        {
            not_supported,
            binary_little_endian,
            ascii
        }

        static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.NX: return 4;
                case DataProperty.NY: return 4;
                case DataProperty.NZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
                case DataProperty.LVI: return 13;
            }
            return 0;
        }

        public class DataHeader
        {
            public List<DataProperty> properties = new List<DataProperty>();
            public PlyFormat format;
            public int vertexCount = 0;
            public bool hasNormals;
            public int faceCount = 0;
        }

        public class DataBody
        {
            public List<Vector3> vertices;
            public List<System.Numerics.Vector3> normals;
            public List<Color32> colors;
            public int[] faces;

            public DataBody(int vertexCount, int faceCount, bool hasNormals)
            {
                faces = new int[faceCount * 3];
                vertices = new List<Vector3>(vertexCount);
                if (hasNormals) normals = new List<System.Numerics.Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(new Color32(r, g, b, a));
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a, float nx, float ny, float nz
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(new Color32(r, g, b, a));
                normals.Add(new System.Numerics.Vector3(nx, ny, nz));
            }
        }

        #endregion

        #region Reader implementation

        Mesh ImportAsMesh(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));

                var mesh = new Mesh();
                mesh.name = Path.GetFileNameWithoutExtension(path);

                mesh.indexFormat = header.vertexCount > 65535 ?
                    IndexFormat.UInt32 : IndexFormat.UInt16;

                mesh.SetVertices(body.vertices);
                mesh.SetColors(body.colors);

                mesh.SetIndices(
                    Enumerable.Range(0, header.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );

                mesh.UploadMeshData(true);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        PointCloudData ImportAsPointCloudData(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<PointCloudData>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        BakedPointCloud ImportAsBakedPointCloud(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));
                var data = ScriptableObject.CreateInstance<BakedPointCloud>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        public DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            switch (line)
            {
                case "format binary_little_endian 1.0":
                    data.format = PlyFormat.binary_little_endian;
                    break;
                case "format ascii 1.0":
                    data.format = PlyFormat.ascii;
                    break;
                default:
                    throw new ArgumentException("Invalid data format ('" + line + "').");
            }

            // Read header contents.
            int count = 0;
            for (var skip = false; ;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (count < 30)
                {
                    count++;
                }
                if (line.StartsWith("end_header")) break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else if (col[1] == "face")
                    {
                        data.faceCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices and faces
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    if (col.Length == 3)
                    {
                        switch (col[2])
                        {
                            case "red": prop = DataProperty.R8; break;
                            case "green": prop = DataProperty.G8; break;
                            case "blue": prop = DataProperty.B8; break;
                            case "alpha": prop = DataProperty.A8; break;
                            case "x": prop = DataProperty.SingleX; break;
                            case "y": prop = DataProperty.SingleY; break;
                            case "z": prop = DataProperty.SingleZ; break;
                            case "nx": prop = DataProperty.NX; data.hasNormals = true; break;
                            case "ny": prop = DataProperty.NY; data.hasNormals = true; break;
                            case "nz": prop = DataProperty.NZ; data.hasNormals = true; break;
                        }
                    }
                    else if (col.Length == 5)
                    {
                        if (col[4] == "vertex_indices") prop = DataProperty.LVI;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                                col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int" || col[1] == "uint" || col[1] == "float" ||
                                col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64" || col[1] == "uint64" ||
                                col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "list")
                    {

                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            if (data.format == PlyFormat.binary_little_endian) reader.BaseStream.Position = readCount;

            return data;
        }

        public DataBody ReadDataBody(DataHeader header, byte[] data, long offset)
        {
            var body = new DataBody(header.vertexCount, header.faceCount, header.hasNormals);

            float x = 0, y = 0, z = 0;
            float nx = 0, ny = 0, nz = 0;
            byte r = 255, g = 255, b = 255, a = 255;
            int bt = (int)offset;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = data[bt++]; break;
                        case DataProperty.G8: g = data[bt++]; break;
                        case DataProperty.B8: b = data[bt++]; break;
                        case DataProperty.A8: a = data[bt++]; break;

                        case DataProperty.R16: r = (byte)(BitConverter.ToUInt16(data, bt) >> 8); bt += 2; break;
                        case DataProperty.G16: g = (byte)(BitConverter.ToUInt16(data, bt) >> 8); bt += 2; break;
                        case DataProperty.B16: b = (byte)(BitConverter.ToUInt16(data, bt) >> 8); bt += 2; break;
                        case DataProperty.A16: a = (byte)(BitConverter.ToUInt16(data, bt) >> 8); bt += 2; break;

                        case DataProperty.SingleX: x = BitConverter.ToSingle(data, bt); bt += 4; break;
                        case DataProperty.SingleY: y = BitConverter.ToSingle(data, bt); bt += 4; break;
                        case DataProperty.SingleZ: z = BitConverter.ToSingle(data, bt); bt += 4; break;

                        case DataProperty.NX: nx = BitConverter.ToSingle(data, bt); bt += 4; break;
                        case DataProperty.NY: ny = BitConverter.ToSingle(data, bt); bt += 4; break;
                        case DataProperty.NZ: nz = BitConverter.ToSingle(data, bt); bt += 4; break;

                        case DataProperty.DoubleX: x = (float)BitConverter.ToDouble(data, bt); bt += 8; break;
                        case DataProperty.DoubleY: y = (float)BitConverter.ToDouble(data, bt); bt += 8; break;
                        case DataProperty.DoubleZ: z = (float)BitConverter.ToDouble(data, bt); bt += 8; break;

                        case DataProperty.Data8: bt++; break;
                        case DataProperty.Data16: bt += 2; break;
                        case DataProperty.Data32: bt += 4; break;
                        case DataProperty.Data64: bt += 8; break;
                    }
                }

                if (header.hasNormals) body.AddPoint(x, y, z, r, g, b, a, nx, ny, nz);
                else body.AddPoint(x, y, z, r, g, b, a);
            }
            if (header.properties.Contains(DataProperty.LVI))
                for (var i = 0; i < header.faceCount; i++)
                {
                    bt++;
                    body.faces[i * 3 + 2] = BitConverter.ToInt32(data, bt); bt += 4;
                    body.faces[i * 3 + 1] = BitConverter.ToInt32(data, bt); bt += 4;
                    body.faces[i * 3 + 0] = BitConverter.ToInt32(data, bt); bt += 4;
                }

            return body;
        }

        public DataBody ReadDataBody(DataHeader header, StreamReader reader)
        {
            var body = new DataBody(header.vertexCount, header.faceCount, header.hasNormals);

            float x = 0, y = 0, z = 0;
            float nx = 0, ny = 0, nz = 0;
            byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                string _line = reader.ReadLine();
                string[] line = _line.Split(' ');
                int id = 0;
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: case DataProperty.R16: r = byte.Parse(line[id++], CultureInfo.InvariantCulture); break;
                        case DataProperty.G8: case DataProperty.G16: g = byte.Parse(line[id++], CultureInfo.InvariantCulture); ; break;
                        case DataProperty.B8: case DataProperty.B16: b = byte.Parse(line[id++], CultureInfo.InvariantCulture); ; break;
                        case DataProperty.A8: case DataProperty.A16: a = byte.Parse(line[id++], CultureInfo.InvariantCulture); ; break;

                        case DataProperty.SingleX: case DataProperty.DoubleX: x = float.Parse(line[id++], CultureInfo.InvariantCulture); break;
                        case DataProperty.SingleY: case DataProperty.DoubleY: y = float.Parse(line[id++], CultureInfo.InvariantCulture); break;
                        case DataProperty.SingleZ: case DataProperty.DoubleZ: z = float.Parse(line[id++], CultureInfo.InvariantCulture); break;

                        case DataProperty.NX: nx = float.Parse(line[id++], CultureInfo.InvariantCulture); break;
                        case DataProperty.NY: ny = float.Parse(line[id++], CultureInfo.InvariantCulture); break;
                        case DataProperty.NZ: nz = float.Parse(line[id++], CultureInfo.InvariantCulture); break;
                    }
                }

                if (header.hasNormals) body.AddPoint(x, y, z, r, g, b, a, nx, ny, nz);
                else body.AddPoint(x, y, z, r, g, b, a);
            }
            if (header.properties.Contains(DataProperty.LVI))
                for (var i = 0; i < header.faceCount; i++)
                {
                    string[] line = reader.ReadLine().Split(' ');
                    body.faces[i * 3 + 2] = int.Parse(line[1], CultureInfo.InvariantCulture);
                    body.faces[i * 3 + 1] = int.Parse(line[2], CultureInfo.InvariantCulture);
                    body.faces[i * 3 + 0] = int.Parse(line[3], CultureInfo.InvariantCulture);
                }

            return body;
        }

        public DataBody ReadDataBody(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody(header.vertexCount, header.faceCount, header.hasNormals);

            float x = 0, y = 0, z = 0;
            float nx = 0, ny = 0, nz = 0;
            byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = reader.ReadByte(); break;
                        case DataProperty.G8: g = reader.ReadByte(); break;
                        case DataProperty.B8: b = reader.ReadByte(); break;
                        case DataProperty.A8: a = reader.ReadByte(); break;

                        case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                        case DataProperty.SingleX: x = reader.ReadSingle(); break;
                        case DataProperty.SingleY: y = reader.ReadSingle(); break;
                        case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                        case DataProperty.NX: nx = reader.ReadSingle(); break;
                        case DataProperty.NY: ny = reader.ReadSingle(); break;
                        case DataProperty.NZ: nz = reader.ReadSingle(); break;

                        case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                        case DataProperty.Data8: reader.ReadByte(); break;
                        case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                        case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                        case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                    }
                }

                if (header.hasNormals) data.AddPoint(x, y, z, r, g, b, a, nx, ny, nz);
                else data.AddPoint(x, y, z, r, g, b, a);
            }
            Log.CaptureTime();
            if (header.properties.Contains(DataProperty.LVI))
                for (var i = 0; i < header.faceCount; i++)
                {
                    reader.BaseStream.Position += 1;
                    data.faces[i * 3 + 0] = reader.ReadInt32();
                    data.faces[i * 3 + 1] = reader.ReadInt32();
                    data.faces[i * 3 + 2] = reader.ReadInt32();
                }
            Log.GetTime();
            Log.PrintAverage();

            return data;
        }
    }
    #endregion
}
