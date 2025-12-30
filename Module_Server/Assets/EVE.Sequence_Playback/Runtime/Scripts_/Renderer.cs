using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.VFX;

namespace EVE
{
    [ExecuteAlways]
    public partial class Renderer : Singleton<Renderer>
    {
        public List<SequenceTracker> sequences;
        public Material _Material;
        [HideInInspector, SerializeField] public string sUnlit, sUnlit_VertexColor, sLit, sLit_VertexColor, sWireframe, sWireframeShaded;
        public Shader Unlit, Unlit_VertexColor, Lit, Lit_VertexColor, Wireframe, WireframeShaded, currentShader;
        public VisualEffectAsset[] vfxFilter;
        public PointVFX[] points;
        
        public float pointSize = 20;
        public int currentVfx = 0;
        public bool setWireframe, PointIMG = true, useTexture = true;

        private static string[] textureProperties = new string[] { "_MainTex", "_BaseMap", "_BaseColorMap" };

        [ContextMenu("RememberShaders")]
        public void RememberShaders()
        {
            sUnlit = Unlit.name;
            sUnlit_VertexColor = Unlit_VertexColor.name;
            sLit = Lit.name;
            sLit_VertexColor = Lit_VertexColor.name;
            sWireframe = Wireframe.name;
            sWireframeShaded = WireframeShaded.name;
        }
        public void AssignShaders()
        {
            Unlit = Shader.Find(sUnlit);
            Unlit_VertexColor = Shader.Find(sUnlit_VertexColor);
            Lit = Shader.Find(sLit);
            Lit_VertexColor = Shader.Find(sLit_VertexColor);
            Wireframe = Shader.Find(sWireframe);
            WireframeShaded = Shader.Find(sWireframeShaded);
        }
        private void Awake()
        {
            AssignShaders();
        }
        private void Update()
        {
            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i].s == null)
                {
                    UnLoad(sequences[i].fp);
                    sequences.RemoveAt(i--);
                }
            }
        }
        public void UnLoad(string sequenceFramePath)
        {
            if (Application.platform != RuntimePlatform.Android && Directory.Exists(sequenceFramePath))
            {
                Directory.Delete(sequenceFramePath, true); //User might want to remove this line if they don't want to delete the preloaded data on object destruction
                File.Delete($"{sequenceFramePath}.meta");
            }
        }

        #region Rendering
        public void RenderUnlitTexture()
        {
            useTexture = true;
            Render(Unlit);
        }
        public void RenderLitTexture()
        {
            useTexture = true;
            Render(Lit);
        }
        public void RenderLitNoTexture()
        {
            useTexture = false;
            Render(Lit);
        }
        public void RenderWireframe()
        {
            Render(Wireframe, true);
        }
        public void RenderWireframeShaded()
        {
            Render(WireframeShaded, true);
        }

        private void ToSequence(Action<Sequence> action)
        {
            foreach (var sequence in sequences)
                if(sequence!=null) 
                    action(sequence.s);
        }
        private void Render(Shader s, bool wireframe = false, Action<Material> action = null)
        {
            setWireframe = wireframe;
            currentShader = s;
            Render(action);
        }
        public  void Render(Action<Material> action = null)
        {
            ToSequence(sequence =>
            {
                for (int o = 0; o < sequence.transform.childCount; o++)
                {
                    var child = sequence.transform.GetChild(o);
                    if (!child.gameObject.activeSelf) continue;
                    RenderMaterial(sequence, o, action);
                }
            });
        }
        public void RenderMaterial(Sequence seq, int id, Action<Material> action = null)
        {
            bool textureExists = seq.textures != null && seq.textures.Count > id;
            if (currentShader == Unlit && !textureExists) ToMaterial(m => m.shader = Unlit_VertexColor);
            else if (currentShader == Lit && !textureExists) ToMaterial(m => m.shader = Lit_VertexColor);
            else ToMaterial(m => m.shader = currentShader);
            
            if (textureExists)
            {
                if (action != null) ToMaterial(m => action.Invoke(m));
                Texture t = useTexture ? seq.textures[id] : null;
                ToMaterial(m => { if (m.shader == Unlit) m.SetTexture("_UnlitColorMap", t); });
                foreach (var tp in textureProperties) ToMaterial(m => { if (m.HasProperty(tp)) m.SetTexture(tp, t); });
            }
            if (setWireframe) ToMaterial(m => _SetWireframe(m));
        }
        public void ToMaterial(Action<Material> action) {
            ToSequence(sequence => action(sequence.Material));
        }
        private void _SetWireframe(Material mat)
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_MaxTriSize", 200);
        }
        #endregion

        #region VFX
        public void VFXFilter(int id)
        {
            if (vfxFilter.Length > id && id >= 0 && vfxFilter[id] != null)
            {
                currentVfx = id;
                if (id == 0) VFXFilterPoint();
                else VFXFilter(vfxFilter[id]);

                if (id == 0 || id == 8) VFXAction(x => { if (x.HasFloat("Size")) x.SetFloat("Size", pointSize); });
            }
        }
        public VisualEffectAsset GetPointVFX(int size, string path = "")
        {
            foreach (var p in points) if (p.Capacity >= size) return PointIMG ? p.vfxIMG : p.vfx;
            Debug.Log("SIZE IS TOO BIG FOR POINT VFX\nSize:" + size + "\nFile:" + path);
            return points[points.Length - 1].vfx;
        }
        public void VFXFilterPoint()
        {
            ToSequence(sequence =>
            {
                var ve = sequence.GetComponent<VisualEffect>();
                if (ve != null) ve.visualEffectAsset = GetPointVFX(sequence.framePointCount * 2);
            });
        }
        public void VFXFilter(VisualEffectAsset vfx)
        {
            ToSequence(sequence =>
            {
                var ve = sequence.GetComponent<VisualEffect>();
                if (ve != null) ve.visualEffectAsset = vfx;
            });
        }
        public void VFXAction(Action<VisualEffect> action)
        {
            if (action == null) return;
            ToSequence(sequence =>
            {
                var ve = sequence.GetComponent<VisualEffect>();
                if (ve != null) action.Invoke(ve);
            });
        }
        #endregion
    }

    [Serializable]
    public class PointVFX
    {
        public VisualEffectAsset vfx, vfxIMG;
        public int Capacity;
    }
}