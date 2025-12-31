using Microsoft.MixedReality.WebRTC.Unity;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using UnityEngine;

public class CaculateReprojection2D
{
    public float Object_Screen_norm_x { get; set; }
    public float Object_Screen_norm_y { get; set; }
    public float Object_ScreenPosition_z { get; set; }
    public float Object_OriginalPosition_x { get; set; }
    public float Object_OriginalPosition_y { get; set; }
    public float Object_OriginalPosition_z { get; set; }
}

public class CalcPositionErrorOurs : MonoBehaviour
{
    [SerializeField]
    PeerConnection Getdata;
    [SerializeField]
    Camera cam;
    [SerializeField]
    ControlUserPosition control_User_Position;
    [SerializeField]
    GameObject gameobject;

    private float reprojection_error_3D;
    private float reprojection_error_2D;

    DataTable reprojectionerrortocsv = CreatCSVTable();
    private static string currentCSVPath; // 儲存當前CSV檔案路徑
    private static bool isFirstRun = true; // 追蹤是否為第一次執行

    void OnEnable()
    {
        // 只在第一次執行時建立新的CSV檔案
        if (isFirstRun)
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string logsDir = Path.Combine(projectRoot, "Logs", "streaming");
            Directory.CreateDirectory(logsDir);
            currentCSVPath = Path.Combine(logsDir, $"3DReprojection_{timestamp}.csv");
            isFirstRun = false;
            Debug.Log($"建立新的CSV檔案: {currentCSVPath}");
        }
    }

    void OnDisable()
    {
        // 當編輯器停止執行時，重置標記
        isFirstRun = true;
    }

    void Update()
    {
        string Received_json = Getdata.GetReceivedDataFromServer();
        Debug.Log($"收到原始JSON數據: {Received_json}");

        if (Received_json == "")
        {
            Debug.Log("沒有收到任何數據");
        }
        else
        {
            DataRow dataraw = reprojectionerrortocsv.NewRow();
            CaculateReprojection2D object_reprojection = JsonConvert.DeserializeObject<CaculateReprojection2D>(Received_json);

            Debug.Log($"解析後的數據:");
            Debug.Log($"Screen_norm_x: {object_reprojection.Object_Screen_norm_x}");
            Debug.Log($"Screen_norm_y: {object_reprojection.Object_Screen_norm_y}");
            Debug.Log($"ScreenPosition_z: {object_reprojection.Object_ScreenPosition_z}");
            Debug.Log($"OriginalPosition_x: {object_reprojection.Object_OriginalPosition_x}");
            Debug.Log($"OriginalPosition_y: {object_reprojection.Object_OriginalPosition_y}");
            Debug.Log($"OriginalPosition_z: {object_reprojection.Object_OriginalPosition_z}");

            Vector3 originalpoint = new Vector3(object_reprojection.Object_OriginalPosition_x, object_reprojection.Object_OriginalPosition_y, object_reprojection.Object_OriginalPosition_z);
            Debug.Log($"原始點座標: {originalpoint}");

            AddDatatoDataTable(dataraw, "raw_x", originalpoint.x);
            AddDatatoDataTable(dataraw, "raw_y", originalpoint.y);
            AddDatatoDataTable(dataraw, "raw_z", originalpoint.z);

            if (object_reprojection.Object_Screen_norm_x != float.NaN && object_reprojection.Object_Screen_norm_y != float.NaN)
            {
                float straming_x = (gameobject.transform.localScale.x * object_reprojection.Object_Screen_norm_x) - (gameobject.transform.localScale.x / 2);
                float straming_y = (gameobject.transform.localScale.y * object_reprojection.Object_Screen_norm_y) - (gameobject.transform.localScale.y / 2);
                Debug.Log($"計算的streaming偏移量: x={straming_x}, y={straming_y}");

                Vector3 streamingpoint = new Vector3(object_reprojection.Object_OriginalPosition_x + straming_x, object_reprojection.Object_OriginalPosition_y + straming_y, object_reprojection.Object_OriginalPosition_z);
                Debug.Log($"Streaming點座標: {streamingpoint}");

                reprojection_error_3D = Vector3.Distance(streamingpoint, originalpoint);
                Vector3 original_3Dto2D = cam.WorldToScreenPoint(originalpoint);
                Vector3 streaming_3Dto2D = cam.WorldToScreenPoint(streamingpoint);

                Debug.Log($"原始點轉換到螢幕座標: {original_3Dto2D}");
                Debug.Log($"Streaming點轉換到螢幕座標: {streaming_3Dto2D}");

                Vector2 original_2D = new Vector2(original_3Dto2D.x / Screen.width, original_3Dto2D.y / Screen.height);
                Vector2 streaming_2D = new Vector2(streaming_3Dto2D.x / Screen.width, streaming_3Dto2D.y / Screen.height);
                Debug.Log($"原始點歸一化2D座標: {original_2D}");
                Debug.Log($"Streaming點歸一化2D座標: {streaming_2D}");

                reprojection_error_2D = Vector2.Distance(original_2D, streaming_2D);

                Debug.Log($"3D重投影誤差: {reprojection_error_3D}");
                Debug.Log($"2D重投影誤差: {reprojection_error_2D}");

                AddDatatoDataTable(dataraw, "stream_x", streamingpoint.x);
                AddDatatoDataTable(dataraw, "stream_y", streamingpoint.y);
                AddDatatoDataTable(dataraw, "stream_z", streamingpoint.z);
            }
            else
            {
                Debug.Log("Screen_norm_x 或 Screen_norm_y 為 NaN，設置誤差為0");
                reprojection_error_3D = 0f;
                reprojection_error_2D = 0f;
                AddDatatoDataTable(dataraw, "stream_x", 0f);
                AddDatatoDataTable(dataraw, "stream_y", 0f);
                AddDatatoDataTable(dataraw, "stream_z", 0f);
            }

            AddDatatoDataTable(dataraw, "reprojection_error_3D", reprojection_error_3D);
            AddDatatoDataTable(dataraw, "reprojection_error_2D", reprojection_error_2D);
            reprojectionerrortocsv.Rows.Add(dataraw);

            Debug.Log("準備儲存數據到CSV...");
            SaveReprojectionerror();
            Debug.Log("數據已儲存到CSV");
        }
    }

    private static DataTable CreatCSVTable()
    {
        // 創儲存位置的資料表
        DataTable dt = new DataTable("Reprojection");

        // 定義每一列需放甚麼資料
        dt.Columns.Add("reprojection_error_3D");
        dt.Columns.Add("reprojection_error_2D");
        dt.Columns.Add("raw_x");
        dt.Columns.Add("raw_y");
        dt.Columns.Add("raw_z");
        dt.Columns.Add("stream_x");
        dt.Columns.Add("stream_y");
        dt.Columns.Add("stream_z");


        // 於第一行加入標籤
        DataRow dr = dt.NewRow();
        dr["reprojection_error_3D"] = "reprojection_error_3D";
        dr["reprojection_error_2D"] = "reprojection_error_2D";
        dr["stream_x"] = "stream_x";
        dr["stream_y"] = "stream_y";
        dr["stream_z"] = "stream_z";
        dr["raw_x"] = "raw_x";
        dr["raw_y"] = "raw_y";
        dr["raw_z"] = "raw_z";
        dt.Rows.Add(dr);
        return dt;
    }

    private void AddDatatoDataTable<T>(DataRow dr, string columname, T value)
    {
        dr[columname] = value;
    }

    private static void SaveCSV(string CSVPath, DataTable mSheet)
    {

        if (mSheet.Rows.Count < 1)
            return;
        int rowCount = mSheet.Rows.Count;
        int colCount = mSheet.Columns.Count;

        StringBuilder stringBuilder = new StringBuilder();

        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < colCount; j++)
            {
                //使用","分割每一個數值
                stringBuilder.Append(mSheet.Rows[i][j] + ",");
            }
            //使用換行符分割每一行
            stringBuilder.Append("\r\n");
        }

        //寫入文件中
        using (FileStream fileStream = new FileStream(CSVPath, FileMode.Create, FileAccess.Write))
        {
            using (TextWriter textWriter = new StreamWriter(fileStream, Encoding.UTF8))
            {
                textWriter.Write(stringBuilder.ToString());
            }
        }
    }

    private static void SaveCSV1(string CSVPath, DataRow mSheet)
    {
        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.Append(mSheet);

        //寫入文件中
        using (FileStream fileStream = new FileStream(CSVPath, FileMode.Create, FileAccess.Write))
        {
            using (TextWriter textWriter = new StreamWriter(fileStream, Encoding.UTF8))
            {
                textWriter.Write(stringBuilder.ToString());
            }
        }
    }
    public void SaveReprojectionerror()
    {
        DataTable raw_data = control_User_Position.GetRawData();
        SaveCSV(currentCSVPath, reprojectionerrortocsv);
    }
    public void SaveReprojectionerror1(DataRow raw_data)
    {
        SaveCSV1(currentCSVPath, raw_data);
    }
}

