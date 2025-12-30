using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EVE
{
    public static class ExtensionMethods
    {
        public static UnityWebRequest BeginReadFile(this string path)
        {
            try
            {
                var www = UnityWebRequest.Get(path);
                www.SendWebRequest();
                return www;
            }
            catch (Exception e)
            {
                Debug.Log(e);
                throw;
            }
        }
        public static byte[] ReadFile(this string path)
        {
            return path.ReadFileAs().data;
        }
        public static DownloadHandler ReadFileAs(this string path)
        {
            try
            {
                var www = UnityWebRequest.Get(path);
                www.SendWebRequest();
                www.WaitToFinish();
                if (www.Failed()) Debug.Log($"{path}\n{www.error}");
                return www.downloadHandler;
            }
            catch (Exception e)
            {
                Debug.Log(e);
                throw;
            }
        }
        public static byte[] ToColorByteArray(this IEnumerable<Vector3> list)
        {
            var a = new byte[list.Count() * 3];
            int i = 0;
            foreach (var v in list)
            {
                a[i++] = (byte)(v.z * 255);
                a[i++] = (byte)(v.y * 255);
                a[i++] = (byte)(v.x * 255);
            }
            return a;
        }
        public static List<float> ToFloatList(this List<Vector3> list)
        {
            var f = new List<float>();
            foreach (var c in list)
            {
                f.Add(c.x);
                f.Add(c.y);
                f.Add(c.z);
            }
            return f;
        }
        public static bool Failed(this UnityWebRequest www)
        {
            return www.result != UnityWebRequest.Result.Success;
        }
        public static void WaitToFinish(this UnityWebRequest www)
        {
            while (www.result == UnityWebRequest.Result.InProgress) { }
        }
        public static void WaitToFinish(this Task task)
        {
            while (task.isRunning()) { }
        }
        public static void ParallelFor(this int l, Action<long> action)
        {
            int o = Mathf.FloorToInt(Mathf.Sqrt(l));
            Parallel.For(0, o, i =>
            {
                i *= o;
                var end = i + o;
                for (long u = i; u < end; u++) action(u);
            });
            Parallel.For(o * o, l, action);
        }
        public static void ParallelFor<T>(this T[] array, Action<long> action, int div = 1)
        {
            (array.Length / div).ParallelFor(action);
        }
        public static void ParallelFor<T>(this List<T> array, Action<long> action, int div = 1)
        {
            (array.Count / div).ParallelFor(action);
        }
        public static float[] ToFloatArray(this byte[] list)
        {
            var array = new float[list.Length / 4];
            unsafe
            {
                fixed (byte* pBuffer = list)
                {
                    float* pSample = (float*)pBuffer;
                    array.ParallelFor(i => array[i] = pSample[i]);
                }
            }
            return array;
        }

        public static void DestroyChildren(this Transform transform)
        {
            while (transform.childCount > 0)
                UnityEngine.Object.DestroyImmediate(transform.GetChild(0).gameObject);
        }
        public static byte[] ToByteArray(this Color32[] list)
        {
            byte[] arr = new byte[list.Length * 3];
            list.ParallelFor(i =>
            {
                arr[i * 3] = list[i].r;
                arr[i * 3 + 1] = list[i].g;
                arr[i * 3 + 2] = list[i].b;
            });
            return arr;
        }
        public static byte[] ToByteArray(this Dictionary<string, List<int>> dict)
        {
            int size = 0;
            foreach (var d in dict)
            {
                size += (2 + d.Value.Count) * 4;
                size += d.Key.Length * 2;
            }
            var a = new byte[size];
            int i = 0;
            foreach (var d in dict)
            {
                foreach (var b in BitConverter.GetBytes(d.Key.Length)) a[i++] = b;
                foreach (var c in d.Key) foreach (var b in BitConverter.GetBytes(c)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(d.Value.Count)) a[i++] = b;
                foreach (var c in d.Value) foreach (var b in BitConverter.GetBytes(c)) a[i++] = b;
            }
            return a;
        }
        public static byte[] ToByteArray(this IEnumerable<Vector3> list)
        {
            var a = new byte[list.Count() * 12];
            int i = 0;
            foreach (var v in list)
            {
                foreach (var b in BitConverter.GetBytes(v.x)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.y)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.z)) a[i++] = b;
            }
            return a;
        }
        public static byte[] ToByteArray(this List<Vector2> list)
        {
            var a = new byte[list.Count * 8];
            int i = 0;
            foreach (var v in list)
            {
                foreach (var b in BitConverter.GetBytes(v.x)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.y)) a[i++] = b;
            }
            return a;
        }
        public static byte[] ToByteArray(this string str)
        {
            var a = new byte[sizeof(int) + sizeof(char) * str.Length];
            int i = 0;
            foreach (var b in BitConverter.GetBytes(str.Length)) a[i++] = b;
            foreach (var c in str)
                foreach (var b in BitConverter.GetBytes(c)) a[i++] = b;
            return a;
        }
        public static byte[] ToByteArray(this List<Color> list)
        {
            var a = new byte[list.Count * 16];
            int i = 0;
            foreach (var v in list)
            {
                foreach (var b in BitConverter.GetBytes(v.r)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.g)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.b)) a[i++] = b;
                foreach (var b in BitConverter.GetBytes(v.a)) a[i++] = b;
            }
            return a;
        }
        public static string ToStringFromBytes(this byte[] list)
        {
            string str = "";
            int l = BitConverter.ToInt32(list, 0);
            for (int i = 0; i < l; i++) str += BitConverter.ToChar(list, sizeof(int) + i * sizeof(char));
            return str;
        }
        public static Dictionary<string, int[]> ToFaceDictionary(this byte[] list)
        {
            var dict = new Dictionary<string, int[]>();
            int i = 0;
            char[] key;
            while (i < list.Length)
            {
                var sl = BitConverter.ToInt32(list, i); i += 4;
                key = new char[sl];
                for (int si = 0; si < sl; si++) { key[si] = BitConverter.ToChar(list, i); i += 2; }
                var lc = BitConverter.ToInt32(list, i); i += 4;
                var value = new int[lc];
                Parallel.For(0, lc, li => value[li] = BitConverter.ToInt32(list, i + li * 4));
                i += lc * 4;
                dict.Add(new string(key), value);
            }

            return dict;
        }
        public static Vector3[] ToVector3Array(this byte[] list)
        {
            var a = new Vector3[list.Length / 12];
            Parallel.For(0, a.Length, i =>
            {
                a[i] = new Vector3(
                    BitConverter.ToSingle(list, i * 12),
                    BitConverter.ToSingle(list, i * 12 + 4),
                    BitConverter.ToSingle(list, i * 12 + 8)
                );
            });
            return a;
        }
        public static Color[] ToColorArray(this byte[] list)
        {
            var a = new Color[list.Length / 16];
            Parallel.For(0, a.Length, i =>
            {
                a[i] = new Color(
                    BitConverter.ToSingle(list, i * 16),
                    BitConverter.ToSingle(list, i * 16 + 4),
                    BitConverter.ToSingle(list, i * 16 + 8),
                    BitConverter.ToSingle(list, i * 16 + 12)
                );
            });
            return a;
        }
        public static Vector2[] ToVector2Array(this byte[] list)
        {
            var a = new Vector2[list.Length / 8];
            Parallel.For(0, a.Length, i =>
            {
                a[i] = new Vector2(
                    BitConverter.ToSingle(list, i * 8),
                    BitConverter.ToSingle(list, i * 8 + 4)
                );
            });
            return a;
        }
        public static bool isRunning(this Task task)
        {
            return task != null && !task.IsCanceled && !task.IsCompleted && !task.IsFaulted;
        }
        public static void WaitToFinish(this List<Task> list)
        {
            while (list.Count > 0) if (!list[0].isRunning()) list.RemoveAt(0);
        }
        public static Vector3 ToVector3(this System.Numerics.Vector3 list)
        {
            return new Vector3(list.X, list.Y, list.Z);
        }
        public static int getNumbersAtEnd(this string name)
        {
            if (int.TryParse(string.Concat(Path.GetFileNameWithoutExtension(name).ToArray().Reverse().TakeWhile(char.IsNumber).Reverse()), out int result))
                return result;
            else return -1;
        }
        public static T Instantiate<T>(this GameObject gameObject, Transform parent = null)
        {
            GameObject go;
            if (parent == null)
                go = UnityEngine.Object.Instantiate(gameObject);
            else
                go = UnityEngine.Object.Instantiate(gameObject, parent);
            return go.GetComponent<T>();
        }
        public static void ResetDirectory(this string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
            while (true)
                try
                {
                    Directory.CreateDirectory(path);
                    return;
                }
                catch (Exception) { }
        }
        public static void Destroy(this UnityEngine.Object obj)
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
        public static List<System.Numerics.Vector3> ToNumericVector3List(this List<Vector3> list)
        {
            var v3 = new List<System.Numerics.Vector3>();
            foreach (var v in list) v3.Add(new System.Numerics.Vector3(v.x, v.y, v.z));
            return v3;
        }
        public static List<System.Numerics.Vector3> ToVector3(this List<Color32> list)
        {
            var v3 = new List<System.Numerics.Vector3>();
            foreach (var c in list)
                v3.Add(new System.Numerics.Vector3(c.r, c.g, c.b) / 255f);
            return v3;
        }
        public static List<System.Numerics.Vector3> ToColorVector3List(this byte[] list)
        {
            var a = new List<System.Numerics.Vector3>();
            for (int i = 0; i < list.Length / 3; i++)
            {
                var ai = System.Numerics.Vector3.Zero;
                ai.X = list[i * 3 + 2] / 255f;
                ai.Y = list[i * 3 + 1] / 255f;
                ai.Z = list[i * 3 + 0] / 255f;
                a.Add(ai);
            }
            return a;
        }
        public static List<Vector3> ToVector3List(this byte[] list)
        {
            var a = new List<Vector3>(list.Count() / 12);
            for (int i = 0; i < a.Capacity; i++) a.Add(Vector3.zero);
            Parallel.For(0, a.Count, i =>
            {
                a[i] = new Vector3(
                    BitConverter.ToSingle(list, i * 12),
                    BitConverter.ToSingle(list, i * 12 + 4),
                    BitConverter.ToSingle(list, i * 12 + 8)
                    );
            });
            return a;
        }
        public static List<Color> ToColorList(this byte[] list)
        {
            var a = new List<Color>(list.Length / 16);
            for (int i = 0; i < a.Capacity; i++) a.Add(Color.black);
            Parallel.For(0, a.Count, i =>
            {
                a[i] = new Color(
                    BitConverter.ToSingle(list, i * 16),
                    BitConverter.ToSingle(list, i * 16 + 4),
                    BitConverter.ToSingle(list, i * 16 + 8),
                    BitConverter.ToSingle(list, i * 16 + 12)
                );
            });
            return a;
        }
        public static List<Vector2> ToVector2List(this byte[] list)
        {
            var a = new List<Vector2>(list.Count() / 8);
            for (int i = 0; i < a.Capacity; i++) a.Add(Vector2.zero);
            Parallel.For(0, a.Count, i =>
            {
                a[i] = new Vector2(
                    BitConverter.ToSingle(list, i * 8),
                    BitConverter.ToSingle(list, i * 8 + 4)
                    );
            });
            return a;
        }
    }
}