using UnityEngine;
using UnityEngine.UI; // 或是 TMPro

public class TranscriptUI : MonoBehaviour
{
    public Text uiText; // 如果是用 TextMeshPro 就改 TMP_Text

    void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnTranscriptReceived += UpdateText;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnTranscriptReceived -= UpdateText;
        }
    }

    void UpdateText(string content)
    {
        if (uiText != null)
        {
            uiText.text = content;
        }
    }
}