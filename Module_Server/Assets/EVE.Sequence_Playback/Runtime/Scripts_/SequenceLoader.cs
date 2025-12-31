using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace EVE
{
    public class SequenceLoader : MonoBehaviour
    {
        public GameObject sequencePrefab;
        public ComputeShader ComputeShader;

        public string sequencePath = "";
        public bool isGenerating = false;
        public bool useBuffer = true;

        [HideInInspector] public Sequence sequence;

#if UNITY_EDITOR
        public void SelectSequencePath()
        {
            sequencePath = UnityEditor.EditorUtility.OpenFolderPanel("Choose your Obj Sequence location", sequencePath == "" ? Application.dataPath : sequencePath, "");
        }
#endif

        public void GenerateFrames()
        {
            isGenerating = true;
            StartCoroutine(IGenerateFrames());
        }

        public IEnumerator IGenerateFrames()
        {
            {
                var hash = sequencePath.GetHashCode();
                var match = Renderer.I.sequences.FirstOrDefault(x => x.s.hash == hash);
                if (match != default)
                {
                    Renderer.I.UnLoad(match.fp);
                    match.s.gameObject.Destroy();
                }
            }
            {
                sequence = sequencePrefab.Instantiate<Sequence>(transform);
                yield return sequence.LoadSequence(sequencePath, ComputeShader, useBuffer);
            }
            isGenerating = false;
        }

        [ContextMenu("Reset Path")]
        public void ResetPath()
        {
            sequencePath = "";
        }
    }
}