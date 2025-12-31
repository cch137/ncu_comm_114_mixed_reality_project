using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class UpdateGameObjectFromCSV : MonoBehaviour
{
    public string csvFilePath = "Assets/StreamingAssets/User6Dof/UserPositionData(走動).csv";
    [SerializeField] // 讓變數在 Inspector 中可見
    public GameObject targetGameObject; // 要移動的 GameObject

    private List<string[]> csvData = new List<string[]>();
    private int currentRow = 1; // 目前讀取的行數 (從1開始)
    private float updateInterval = 0.005f; // 每5毫秒更新一次
    private float nextUpdateTime = 0f; // 下次更新的時間

    void Start()
    {
        // 檢查目標 GameObject 是否已設定
        if (targetGameObject == null)
        {
            Debug.LogError("請在 Inspector 中設定要移動的 GameObject！");
            enabled = false; // 停用此 script
            return;
        }

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
            Debug.Log($"成功讀入 {csvData.Count} 筆資料");
        }
        else
        {
            Debug.LogError("CSV 檔案不存在：" + csvFilePath);
            enabled = false; // 停用此 script
            return;
        }

        // 設定第一次更新的時間
        nextUpdateTime = Time.time + updateInterval;
    }

    void Update()
    {
        // 再次檢查 targetGameObject 是否為空
        if (targetGameObject == null)
        {
            Debug.LogError("targetGameObject 為空！請在 Inspector 中設定目標物件");
            enabled = false;
            return;
        }

        // 檢查是否到達更新時間
        if (Time.time >= nextUpdateTime)
        {
            if (currentRow < csvData.Count)
            {
                string[] row = csvData[currentRow];

                // 解析 CSV 資料並更新 GameObject
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

                        Debug.Log($"更新第 {currentRow} 行資料：位置({position_x}, {position_y}, {position_z}), 旋轉({rotation_x}, {rotation_y}, {rotation_z})");
                    }
                    catch (System.FormatException ex)
                    {
                        Debug.LogError("解析 CSV 資料時發生錯誤：" + ex.Message + "，請檢查資料格式。資料為：" + string.Join(",", row));
                    }
                }
                else
                {
                    Debug.LogWarning("CSV 資料行數不足，請檢查資料格式。資料為：" + string.Join(",", row));
                }

                currentRow++;
                // 設定下次更新的時間
                nextUpdateTime = Time.time + updateInterval;
            }
            else
            {
                Debug.Log("已讀取完所有 CSV 資料，準備關閉編輯器");
                enabled = false; // 讀取完所有資料後停用此 script

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }
    }
}
//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;
//using UnityEditor;

//public enum UserPositionDataType
//{
//    �T�w,
//    ����,
//    ���ʫ�^��a
//}

//public class UpdateGameObjectFromCSV : MonoBehaviour
//{
//    [SerializeField]
//    public UserPositionDataType dataType = UserPositionDataType.�T�w;

//    public GameObject targetGameObject; // �n��s�� GameObject

//    private List<string[]> csvData = new List<string[]>();
//    private int currentRow = 1; // �q�ĤG��}�lŪ�� (���L���D)
//    private string csvFileName;
//    private float updateInterval = 0.1f; // �C0.1����s�@��
//    private float nextUpdateTime = 0f;
//    public NodeDssSignalerUI nodeDssSignalerUI;
//    void Start()
//    {
//        // �ھڿ�ܪ��ƾ������]�m�ɮצW��
//        switch (dataType)
//        {
//            case UserPositionDataType.�T�w:
//                csvFileName = "UserPositionData(�T�w).csv";
//                break;
//            case UserPositionDataType.����:
//                csvFileName = "UserPositionData(����).csv";
//                break;
//            case UserPositionDataType.���ʫ�^��a:
//                csvFileName = "UserPositionData(���ʫ�^��a).csv";
//                break;
//        }

//        string csvFilePath = Path.Combine(Application.dataPath, "Script", "User6Dof", csvFileName);

//        // Ū�� CSV �ɮ�
//        if (File.Exists(csvFilePath))
//        {
//            using (var reader = new StreamReader(csvFilePath))
//            {
//                reader.ReadLine(); // ���L���D��
//                while (!reader.EndOfStream)
//                {
//                    var line = reader.ReadLine();
//                    var values = line.Split(',');
//                    csvData.Add(values);
//                }
//            }
//            Debug.Log($"���\���J {csvFileName}�A�@ {csvData.Count} ��ƾ�");
//        }
//        else
//        {
//            Debug.LogError($"CSV �ɮפ��s�b: {csvFilePath}");
//            enabled = false; // �����}��
//            return;
//        }

//        // �ˬd�ؼ� GameObject �O�_�w���w
//        if (targetGameObject == null)
//        {
//            Debug.LogError("�Ы��w�n��s�� GameObject");
//            enabled = false; // �����}��
//            return;
//        }
//        nodeDssSignalerUI.StartConnection();
//    }

//    void Update()
//    {
//        if (Time.time >= nextUpdateTime)
//        {
//            if (currentRow < csvData.Count)
//            {
//                string[] row = csvData[currentRow];

//                // �N CSV �ƾ��ഫ�� GameObject
//                if (row.Length >= 6)
//                {
//                    try
//                    {
//                        double position_x = double.Parse(row[0]);
//                        double position_y = double.Parse(row[1]);
//                        double position_z = double.Parse(row[2]);
//                        double rotation_x = double.Parse(row[3]);
//                        double rotation_y = double.Parse(row[4]);
//                        double rotation_z = double.Parse(row[5]);

//                        targetGameObject.transform.position = new Vector3((float)position_x, (float)position_y, (float)position_z);
//                        targetGameObject.transform.rotation = Quaternion.Euler((float)rotation_x, (float)rotation_y, (float)rotation_z);
//                    }
//                    catch (System.FormatException ex)
//                    {
//                        Debug.LogError($"Ū�� CSV �ƾڮɵo�Ϳ��~: {ex.Message}�A��: {string.Join(",", row)}");
//                        currentRow++; // ���L���~��
//                    }
//                }
//                else
//                {
//                    Debug.LogWarning($"CSV ��ƾڤ���: {string.Join(",", row)}");
//                    currentRow++; // ���L���~��
//                }

//                currentRow++;
//            }
//            else
//            {
//                Debug.Log($"�w����Ū�� {csvFileName} ���Ҧ��ƾڡC");
//                enabled = false; // �����}��

//#if UNITY_EDITOR
//                if (Application.isEditor)
//                {
//                    EditorApplication.isPlaying = false;
//                }
//#endif
//            }

//            nextUpdateTime = Time.time + updateInterval;
//        }
//    }
//}