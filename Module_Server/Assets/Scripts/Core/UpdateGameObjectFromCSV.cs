using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public enum UserPositionDataType
{
    固定,
    走動,
    走動後回原地
}

public class UpdateGameObjectFromCSV : MonoBehaviour
{
    [SerializeField]
    public UserPositionDataType dataType = UserPositionDataType.固定;

    public GameObject targetGameObject; // 要更新的 GameObject

    private List<string[]> csvData = new List<string[]>();
    private int currentRow = 1; // 從第二行開始讀取 (跳過標題)
    private string csvFileName;
    private float updateInterval = 0.1f; // 每0.1秒更新一次
    private float nextUpdateTime = 0f;

    void Start()
    {
        // 根據選擇的數據類型設置檔案名稱
        switch (dataType)
        {
            case UserPositionDataType.固定:
                csvFileName = "UserPositionData(固定).csv";
                break;
            case UserPositionDataType.走動:
                csvFileName = "UserPositionData(走動).csv";
                break;
            case UserPositionDataType.走動後回原地:
                csvFileName = "UserPositionData(走動後回原地).csv";
                break;
        }

        // 從 StreamingAssets/User6Dof 讀取（對應到 Assets/StreamingAssets/User6Dof）
        string csvFilePath = Path.Combine(Application.streamingAssetsPath, "User6Dof", csvFileName);

        // 讀取 CSV 檔案
        if (File.Exists(csvFilePath))
        {
            using (var reader = new StreamReader(csvFilePath))
            {
                reader.ReadLine(); // 跳過標題行
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    csvData.Add(values);
                }
            }
            Debug.Log($"成功載入 {csvFileName}，共 {csvData.Count} 行數據");
        }
        else
        {
            Debug.LogError($"CSV 檔案不存在: {csvFilePath}");
            enabled = false; // 關閉腳本
            return;
        }

        // 檢查目標 GameObject 是否已指定
        if (targetGameObject == null)
        {
            Debug.LogError("請指定要更新的 GameObject");
            enabled = false; // 關閉腳本
            return;
        }
    }

    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            if (currentRow < csvData.Count)
            {
                string[] row = csvData[currentRow];

                // 將 CSV 數據轉換為 GameObject
                if (row.Length >= 6)
                {
                    try
                    {
                        double position_x = double.Parse(row[0]);
                        double position_y = double.Parse(row[1]);
                        double position_z = double.Parse(row[2]);
                        double rotation_x = double.Parse(row[3]);
                        double rotation_y = double.Parse(row[4]);
                        double rotation_z = double.Parse(row[5]);

                        targetGameObject.transform.position = new Vector3((float)position_x, (float)position_y, (float)position_z);
                        targetGameObject.transform.rotation = Quaternion.Euler((float)rotation_x, (float)rotation_y, (float)rotation_z);
                    }
                    catch (System.FormatException ex)
                    {
                        Debug.LogError($"讀取 CSV 數據時發生錯誤: {ex.Message}，行: {string.Join(",", row)}");
                        currentRow++; // 跳過錯誤行
                    }
                }
                else
                {
                    Debug.LogWarning($"CSV 行數據不足: {string.Join(",", row)}");
                    currentRow++; // 跳過錯誤行
                }

                currentRow++;
            }
            else
            {
                Debug.Log($"已完成讀取 {csvFileName} 的所有數據。");
                enabled = false; // 關閉腳本

#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    EditorApplication.isPlaying = false;
                }
#endif
            }

            nextUpdateTime = Time.time + updateInterval;
        }
    }
}