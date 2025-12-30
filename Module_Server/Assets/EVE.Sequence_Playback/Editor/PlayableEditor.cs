using UnityEditor;

namespace EVE
{
    [CustomEditor(typeof(Playable))]
    public class PlayableEditor : Editor
    {
        Playable Playable;
        private void OnEnable()
        {
            Playable = (Playable)target;
        }
    }
}