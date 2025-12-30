using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _I = null;
    public static T I { get {
            if (_I == null)
            {
                var results = FindObjectsOfType<T>();
                if (results.Length > 0) _I = results[0];
            }
            return _I;
        } }
}