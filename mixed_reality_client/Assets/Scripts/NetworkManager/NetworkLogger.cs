using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

// 1. 改成繼承 MonoBehaviour，這樣就可以掛在物件上了
public class NetworkLogger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("是否顯示接收到的詳細 JSON 資料")]
    public bool ShowReceiveDetails = false; // 變成 Inspector 可見的勾選框

    [Tooltip("是否顯示發送出去的詳細 JSON 資料")]
    public bool ShowSendDetails = false;    // 變成 Inspector 可見的勾選框

    // --- 內部變數 ---
    public const int LogFrequency = 100;
    private Dictionary<string, int> _counters = new Dictionary<string, int>();

    private bool IsHighFrequency(string eventName)
    {
        return eventName == "HeadPose" || eventName == "Poses";
    }

    public void LogSend(string eventName, object data)
    {
        // ... (邏輯與之前完全一樣，不用變) ...
        if (IsHighFrequency(eventName))
        {
            if (!_counters.ContainsKey(eventName)) _counters[eventName] = 0;
            _counters[eventName]++;

            if (_counters[eventName] < LogFrequency) return;

            Debug.Log($"[WS Send] Type: {eventName} (已略過 {LogFrequency - 1} 筆高頻資料)");
            _counters[eventName] = 0;
        }
        else
        {
            Debug.Log($"[WS Send] Type: {eventName}");
        }

        if (ShowSendDetails && data != null)
        {
            string detailJson = JsonUtility.ToJson(data);
            Debug.Log($"[WS Send] Data: {detailJson}");
        }
    }

    public void LogReceive(string messageType, string fullJsonData)
    {
        // ... (邏輯與之前完全一樣) ...
        Debug.Log($"[WS Recv] Type: {messageType}");

        if (ShowReceiveDetails)
        {
            Debug.Log($"[WS Recv] Data: {fullJsonData}");
        }
    }

    public void ResetCounters()
    {
        _counters.Clear();
    }
}