using UnityEngine;
using UnityEditor;

namespace EVE
{
    [CustomEditor(typeof(Renderer))]
    public class RendererEditor : Editor
    {
        Renderer t;
        private void OnEnable()
        {
            t = (Renderer)target;
        }
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Render Unlit"))
                t.RenderUnlitTexture();
            if (GUILayout.Button("Render Lit"))
                t.RenderLitTexture();
            if (GUILayout.Button("Render Wireframe"))
                t.RenderWireframe();
            if (GUILayout.Button("Render Shaded-Wireframe"))
                t.RenderWireframeShaded();

            base.OnInspectorGUI();
            EditorUtility.SetDirty(t);
        }
    }
}