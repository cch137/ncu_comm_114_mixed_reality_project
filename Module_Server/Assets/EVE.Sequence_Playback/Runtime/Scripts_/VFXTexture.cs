using Pcx;
using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace EVE
{
    [Serializable]
    public class VFXTexture
    {
        public byte[] rgbData;
        public int width, k;
        public ComputeShader VFXShader;

        public float framePointCount;
        public Matrix4x4 scaleMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1));
        public Texture pt, ct;

        public VFXTexture(ComputeShader cShader)
        {
            VFXShader = cShader;
            k = VFXShader.FindKernel("TransferData");
        }
        public static float[] ApplyMatrix(float[] points, Matrix4x4 m4x4)
        {
            points.ParallelFor(i =>
            {
                var result = m4x4.MultiplyPoint3x4(new Vector3(points[i * 3], points[i * 3 + 1], points[i * 3 + 2]));
                points[i * 3] = result.x;
                points[i * 3 + 1] = result.y;
                points[i * 3 + 2] = result.z;
            }, 3);
            return points;
        }
        public void PointcloudToTexture(byte[] p, long dataLength)
        {
            try
            {
                Task tCol, tPos;
                uint[] colors = null;
                float[] pointData = null;
                ComputeBuffer colBuffer, posBuffer;
                framePointCount = dataLength;
                
                colBuffer = new ComputeBuffer((int)framePointCount * 3, sizeof(uint), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                NativeArray<uint> colArray = colBuffer.BeginWrite<uint>(0, (int)framePointCount * 3);
                tCol = Task.Run(() =>
                {
                    colors = new uint[(int)framePointCount * 3];
                    ((int)framePointCount).ParallelFor(i => {
                        long _i = (long)(dataLength / framePointCount * i) * 3;
                        i *= 3;
                        colors[i] = rgbData[_i];
                        colors[i + 1] = rgbData[_i + 1];
                        colors[i + 2] = rgbData[_i + 2];
                    });
                    colArray.CopyFrom(colors);
                });
                
                posBuffer = new ComputeBuffer((int)framePointCount * 3, sizeof(float), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
                NativeArray<float> posArray = posBuffer.BeginWrite<float>(0, (int)framePointCount * 3);
                tPos = Task.Run(() =>
                {
                    pointData = p.ToFloatArray();
                    pointData = ApplyMatrix(pointData, scaleMatrix);
                    posArray.CopyFrom(pointData);
                });
                
                VFXShader.SetInt("VertexCount", (int)framePointCount);
                VFXShader.SetFloat("PointLimit", framePointCount);

                width = Mathf.CeilToInt(Mathf.Sqrt(framePointCount));
                VFXShader.SetInt("Width", width);
                

                if (pt != null) (pt as RenderTexture).Release();
                pt = new RenderTexture(width, width, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true, filterMode = FilterMode.Point };
                (pt as RenderTexture).Create();
                VFXShader.SetTexture(k, "PositionMap", pt);

                if (ct != null) (ct as RenderTexture).Release();
                ct = new RenderTexture(width, width, 0, RenderTextureFormat.ARGB32) { enableRandomWrite = true, filterMode = FilterMode.Point };
                (ct as RenderTexture).Create();
                VFXShader.SetTexture(k, "ColorMap", ct);


                tPos.WaitToFinish();
                posBuffer.EndWrite<float>((int)framePointCount * 3);
                VFXShader.SetBuffer(k, "PositionBuffer", posBuffer);

                tCol.WaitToFinish();
                colBuffer.EndWrite<uint>((int)framePointCount * 3);
                VFXShader.SetBuffer(k, "ColorBuffer", colBuffer);


                VFXShader.Dispatch(k, width / 7, width / 7, 1);

                posBuffer.Release();
                colBuffer.Release();
            }
            catch (Exception e)
            {
                Debug.Log(e);
                if (pt != null) (pt as RenderTexture).Release();
                if (ct != null) (ct as RenderTexture).Release();
                framePointCount = 0;
            }
        }
        public void LoadFromFiles(string ptPath, string ctPath)
        {
            var p_ = ptPath.BeginReadFile();
            var c_ = ctPath.BeginReadFile();
            while (p_.result == UnityEngine.Networking.UnityWebRequest.Result.InProgress) { }
            if (p_.result != UnityEngine.Networking.UnityWebRequest.Result.Success) return;
            var p = p_.downloadHandler.data;
            while (c_.result == UnityEngine.Networking.UnityWebRequest.Result.InProgress) { }
            if (c_.result != UnityEngine.Networking.UnityWebRequest.Result.Success) return;
            rgbData = c_.downloadHandler.data;

            PointcloudToTexture(p, p.Length / 12);
        }
    }
}