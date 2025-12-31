using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class NetworkTrafficMonitor : MonoBehaviour
{
    [SerializeField]
    private string expMethod = "EXP";

    private Dictionary<string, TrafficStats> trafficStats = new Dictionary<string, TrafficStats>();
    private TrafficStats totalStats = new TrafficStats { streamName = "總和" };
    private float updateInterval = 1.0f;
    private float lastUpdateTime;
    private string individualLogPath;
    private string totalLogPath;
    private StreamWriter individualLogWriter;
    private StreamWriter totalLogWriter;
    private bool isLogging = false;
    private UpdateGameObjectFromCSV userPositionController;

    public class TrafficStats
    {
        public long totalBytesSent;           // 壓縮後的傳輸量
        public long totalBytesReceived;
        public long totalRawBytesSent;        // 壓縮前的原始數據量
        public float currentBitrate;          // 壓縮後的位元率
        public float currentRawBitrate;       // 壓縮前的位元率
        public float compressionRatio;        // 壓縮比
        public DateTime lastUpdateTime;
        public string streamName;
        public long lastBytesSent;            // 上次記錄的傳輸量
        public long lastBytesReceived;        // 上次記錄的接收量
        public long lastRawBytesSent;         // 上次記錄的原始數據量
    }

    private void Start()
    {
        lastUpdateTime = Time.time;
        userPositionController = FindObjectOfType<UpdateGameObjectFromCSV>();
        if (userPositionController != null)
        {
            Debug.Log($"找到 UpdateGameObjectFromCSV，當前 dataType: {userPositionController.dataType}");
        }
        else
        {
            Debug.LogError("找不到 UpdateGameObjectFromCSV 組件");
        }
        InitializeLogging();
    }

    private void OnDestroy()
    {
        StopLogging();
    }

    private void InitializeLogging()
    {
        // Use project-root Logs/streaming folder (project root is parent of Assets)
        string baseLogsDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "streaming"));
        string dofType = userPositionController != null ?
            GetDofTypeName(userPositionController.dataType) : "固定";

        Debug.Log($"創建記錄目錄，使用 6DOF 類型: {dofType}");

        string logsDirectory = Path.Combine(baseLogsDirectory, expMethod, dofType);

        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
            Debug.Log($"創建日誌目錄: {logsDirectory}");
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        individualLogPath = Path.Combine(logsDirectory, $"individual_streams_{timestamp}.csv");
        totalLogPath = Path.Combine(logsDirectory, $"total_traffic_{timestamp}.csv");

        try
        {
            individualLogWriter = new StreamWriter(individualLogPath, true, Encoding.UTF8);
            individualLogWriter.WriteLine("時間,串流名稱,連接ID,總發送(位元組),總接收(位元組),當前位元率(bps),原始數據量(位元組),原始位元率(bps),壓縮比");

            totalLogWriter = new StreamWriter(totalLogPath, true, Encoding.UTF8);
            totalLogWriter.WriteLine("時間,總發送(位元組),總接收(位元組),總位元率(bps),總原始數據量(位元組),總原始位元率(bps),平均壓縮比");

            isLogging = true;
            Debug.Log($"開始記錄網路流量到檔案:\n個別串流: {individualLogPath}\n總和數據: {totalLogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"無法創建日誌檔案: {e.Message}");
        }
    }

    private string GetDofTypeName(UserPositionDataType type)
    {
        switch (type)
        {
            case UserPositionDataType.固定:
                return "固定";
            case UserPositionDataType.走動:
                return "走動";
            case UserPositionDataType.走動後回原地:
                return "走動後回原地";
            default:
                return "固定";
        }
    }

    private void StopLogging()
    {
        try
        {
            if (individualLogWriter != null)
            {
                individualLogWriter.Flush();
                individualLogWriter.Close();
            }
            if (totalLogWriter != null)
            {
                totalLogWriter.Flush();
                totalLogWriter.Close();
            }
            isLogging = false;
            Debug.Log($"網路流量記錄已停止\n個別串流檔案: {individualLogPath}\n總和數據檔案: {totalLogPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"關閉日誌檔案時發生錯誤: {e.Message}");
        }
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateTrafficStats();
            lastUpdateTime = Time.time;
        }
    }

    public void AddTraffic(string connectionId, string streamName, long bytesSent, long bytesReceived, long rawBytesSent = 0)
    {
        if (!trafficStats.ContainsKey(connectionId))
        {
            trafficStats[connectionId] = new TrafficStats
            {
                lastUpdateTime = DateTime.Now,
                streamName = streamName
            };
        }

        var stats = trafficStats[connectionId];
        float timeElapsed = (float)(DateTime.Now - stats.lastUpdateTime).TotalSeconds;

        if (timeElapsed > 0)
        {
            // 更新壓縮後的數據
            long bytesSentDelta = bytesSent - stats.lastBytesSent;
            stats.currentBitrate = (bytesSentDelta * 8) / timeElapsed;
            stats.totalBytesSent += bytesSentDelta;
            stats.lastBytesSent = bytesSent;

            // 更新原始數據
            if (rawBytesSent > 0)
            {
                long rawBytesSentDelta = rawBytesSent - stats.lastRawBytesSent;
                stats.currentRawBitrate = (rawBytesSentDelta * 8) / timeElapsed;
                stats.totalRawBytesSent += rawBytesSentDelta;
                stats.lastRawBytesSent = rawBytesSent;

                // 計算壓縮比
                if (rawBytesSentDelta > 0)
                {
                    stats.compressionRatio = (float)bytesSentDelta / rawBytesSentDelta;
                }
            }
        }

        stats.totalBytesReceived += bytesReceived;
    }

    private void UpdateTrafficStats()
    {
        try
        {
            // 更新總和統計
            totalStats.totalBytesSent = 0;
            totalStats.totalBytesReceived = 0;
            totalStats.currentBitrate = 0;
            totalStats.totalRawBytesSent = 0;
            totalStats.currentRawBitrate = 0;
            float totalCompressionRatio = 0f;
            int compressionCount = 0;

            foreach (var stats in trafficStats.Values)
            {
                totalStats.totalBytesSent += stats.totalBytesSent;
                totalStats.totalBytesReceived += stats.totalBytesReceived;
                totalStats.currentBitrate += stats.currentBitrate;
                totalStats.totalRawBytesSent += stats.totalRawBytesSent;
                totalStats.currentRawBitrate += stats.currentRawBitrate;

                if (stats.compressionRatio > 0)
                {
                    totalCompressionRatio += stats.compressionRatio;
                    compressionCount++;
                }
            }

            // 記錄到檔案
            if (isLogging)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                foreach (var kvp in trafficStats)
                {
                    individualLogWriter.WriteLine($"{timestamp},{kvp.Value.streamName},{kvp.Key}," +
                        $"{kvp.Value.totalBytesSent},{kvp.Value.totalBytesReceived}," +
                        $"{kvp.Value.currentBitrate:F2},{kvp.Value.totalRawBytesSent}," +
                        $"{kvp.Value.currentRawBitrate:F2},{kvp.Value.compressionRatio:F4}");
                }
                individualLogWriter.Flush();

                float avgCompressionRatio = compressionCount > 0 ? totalCompressionRatio / compressionCount : 0f;
                totalLogWriter.WriteLine($"{timestamp},{totalStats.totalBytesSent}," +
                    $"{totalStats.totalBytesReceived},{totalStats.currentBitrate:F2}," +
                    $"{totalStats.totalRawBytesSent},{totalStats.currentRawBitrate:F2}," +
                    $"{avgCompressionRatio:F4}");
                totalLogWriter.Flush();

                if (Application.isEditor)
                {
                    Debug.Log(GetTrafficStats());
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"更新流量統計時發生錯誤: {e.Message}");
        }
    }

    public string GetTrafficStats()
    {
        string statsText = "網路流量統計:\n";

        foreach (var kvp in trafficStats)
        {
            statsText += $"串流名稱: {kvp.Value.streamName}\n";
            statsText += $"連接 {kvp.Key}:\n";
            statsText += $"總發送: {FormatBytes(kvp.Value.totalBytesSent)}\n";
            statsText += $"總接收: {FormatBytes(kvp.Value.totalBytesReceived)}\n";
            statsText += $"當前位元率: {FormatBitrate(kvp.Value.currentBitrate)}\n";
            statsText += $"原始數據量: {FormatBytes(kvp.Value.totalRawBytesSent)}\n";
            statsText += $"原始位元率: {FormatBitrate(kvp.Value.currentRawBitrate)}\n";
            statsText += $"壓縮比: {kvp.Value.compressionRatio:F4}\n\n";
        }

        statsText += $"總和統計:\n";
        statsText += $"總發送: {FormatBytes(totalStats.totalBytesSent)}\n";
        statsText += $"總接收: {FormatBytes(totalStats.totalBytesReceived)}\n";
        statsText += $"總位元率: {FormatBitrate(totalStats.currentBitrate)}\n";
        statsText += $"總原始數據量: {FormatBytes(totalStats.totalRawBytesSent)}\n";
        statsText += $"總原始位元率: {FormatBitrate(totalStats.currentRawBitrate)}\n";

        return statsText;
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number = number / 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private string FormatBitrate(float bitsPerSecond)
    {
        string[] suffixes = { "bps", "Kbps", "Mbps", "Gbps" };
        int counter = 0;
        decimal number = (decimal)bitsPerSecond;
        while (Math.Round(number / 1000) >= 1)
        {
            number = number / 1000;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public float GetTotalBandwidth()
    {
        return totalStats.currentBitrate / 1000000f; // 轉換為 Mbps
    }

    public float GetAverageBandwidth()
    {
        if (trafficStats.Count == 0)
            return 0f;

        float sum = 0f;
        foreach (var stats in trafficStats.Values)
        {
            sum += stats.currentBitrate;
        }
        return (sum / trafficStats.Count) / 1000000f; // 轉換為 Mbps
    }
}