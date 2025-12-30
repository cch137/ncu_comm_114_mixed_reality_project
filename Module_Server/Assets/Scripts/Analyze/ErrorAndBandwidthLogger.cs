using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ErrorAndBandwidthLogger : MonoBehaviour
{
    [SerializeField]
    private string expMethod = "EXP";

    [SerializeField]
    private float loggingInterval = 1.0f; // 每秒記錄一次

    private float nextLogTime;
    private string logFilePath;
    private StreamWriter logWriter;
    private bool isLogging = false;
    private UpdateGameObjectFromCSV userPositionController;
    private NetworkTrafficMonitor trafficMonitor;
    private CalcStreamingObject2DpointOpti errorCalculator;

    private void Start()
    {
        nextLogTime = Time.time;
        userPositionController = FindObjectOfType<UpdateGameObjectFromCSV>();
        trafficMonitor = FindObjectOfType<NetworkTrafficMonitor>();
        errorCalculator = FindObjectOfType<CalcStreamingObject2DpointOpti>();

        if (userPositionController == null || trafficMonitor == null || errorCalculator == null)
        {
            Debug.LogError("找不到必要的組件");
            return;
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

        string logsDirectory = Path.Combine(baseLogsDirectory, expMethod, dofType);

        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(logsDirectory, $"error_bandwidth_{timestamp}.csv");

        try
        {
            logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8);
            logWriter.WriteLine("時間,平均位置誤差(像素),平均頻寬(Mbps),總頻寬(Mbps),平均延遲(ms),最大延遲(ms)");
            isLogging = true;
            Debug.Log($"開始記錄誤差和頻寬數據到檔案: {logFilePath}");
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
            if (logWriter != null)
            {
                logWriter.Flush();
                logWriter.Close();
            }
            isLogging = false;
            Debug.Log($"誤差和頻寬記錄已停止: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"關閉日誌檔案時發生錯誤: {e.Message}");
        }
    }

    private void Update()
    {
        if (Time.time >= nextLogTime && isLogging)
        {
            LogCurrentMetrics();
            nextLogTime = Time.time + loggingInterval;
        }
    }

    private void LogCurrentMetrics()
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            // 獲取平均位置誤差
            float avgError = errorCalculator.GetAverageError();

            // 獲取頻寬數據
            float totalBandwidth = trafficMonitor.GetTotalBandwidth();
            float avgBandwidth = trafficMonitor.GetAverageBandwidth();

            // 獲取延遲數據
            float avgLatency = errorCalculator.GetAverageLatency();
            float maxLatency = errorCalculator.GetMaxLatency();

            // 寫入日誌
            logWriter.WriteLine($"{timestamp},{avgError:F2},{avgBandwidth:F2},{totalBandwidth:F2},{avgLatency:F2},{maxLatency:F2}");
            logWriter.Flush();

            // 在編輯器中顯示即時數據
            if (Application.isEditor)
            {
                Debug.Log($"平均位置誤差: {avgError:F2} 像素, " +
                         $"平均頻寬: {avgBandwidth:F2} Mbps, " +
                         $"總頻寬: {totalBandwidth:F2} Mbps, " +
                         $"平均延遲: {avgLatency:F2}ms, " +
                         $"最大延遲: {maxLatency:F2}ms");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"寫入日誌時發生錯誤: {e.Message}");
        }
    }
}