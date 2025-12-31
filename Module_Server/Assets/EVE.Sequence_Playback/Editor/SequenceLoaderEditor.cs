using UnityEngine;
using UnityEditor;

namespace EVE
{
    [CustomEditor(typeof(SequenceLoader))]
    public class SequenceLoaderEditor : Editor
    {
        SequenceLoader t;
        private void OnEnable()
        {
            t = (SequenceLoader)target;
        }
        public override void OnInspectorGUI()
        {
            t.useBuffer = EditorGUILayout.ToggleLeft("Use Buffer (.obj only)", t.useBuffer);
            t.ComputeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", t.ComputeShader, typeof(ComputeShader), true);
            t.sequencePrefab = (GameObject)EditorGUILayout.ObjectField("Sequence Prefab", t.sequencePrefab, typeof(GameObject), true);
            if (GUILayout.Button("Select Sequence Folder (Folder containing .obj or .ply files)")) t.SelectSequencePath();
            EditorGUILayout.LabelField("Sequence Path: " + (t.sequencePath == "" ? "[Not Selected]" : t.sequencePath));
            if (t.sequencePath != "")
            {
                EditorGUI.BeginDisabledGroup(t.isGenerating);
                if (t.sequence == null) t.isGenerating = false;
                if (GUILayout.Button(t.isGenerating ? "Generating Frames " + t.sequence.generatedFrames + "/" + t.sequence.maxFrames : "Generate Frames")) t.GenerateFrames();
                EditorGUI.EndDisabledGroup();
            }
            EditorUtility.SetDirty(t);
        }
    }
}