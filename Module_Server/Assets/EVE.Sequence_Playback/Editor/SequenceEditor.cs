using UnityEditor;
using UnityEngine;

namespace EVE
{
    [CustomEditor(typeof(Sequence))]
    public class SequenceEditor : Editor
    {
        Sequence t;
        private void OnEnable()
        {
            t = (Sequence)target;
        }
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.BeginHorizontal();
            if (t.playing)
            {
                if (GUILayout.Button("Pause"))
                    t.Pause();
            }
            else if (GUILayout.Button("Play"))
                t.Play();
            if (GUILayout.Button("Stop"))
                t.Stop();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Frame"))
                t.PreviousFrame();
            if (GUILayout.Button("Next Frame"))
                t.NextFrame();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("First Frame"))
                t.FirstFrame();
            if (GUILayout.Button("Last Frame"))
                t.LastFrame();
            EditorGUILayout.EndHorizontal();
            EditorUtility.SetDirty(t);
        }
    }
}