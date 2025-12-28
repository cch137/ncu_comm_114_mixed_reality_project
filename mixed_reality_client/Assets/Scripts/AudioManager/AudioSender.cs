using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class AudioSender : MonoBehaviour
{
    [Header("音訊設定")]
    [Tooltip("錄音頻率 (必須跟 Server / 接收端一致)")]
    public int sampleRate = 16000;

    [Tooltip("音量增益 (放大聲音用，1.0 為原聲)")]
    [Range(1f, 10f)]
    public float micVolume = 1.0f;

    [Tooltip("每累積多少秒的聲音才送出一次? (建議 0.05 ~ 0.1 秒)")]
    public float sendInterval = 0.1f;

    [Header("Debug")]
    public bool showDebugLog = true;

    // 內部狀態變數
    private AudioClip micClip;
    private string deviceName;
    private int lastSamplePosition = 0;
    private bool isTransmitting = false;
    private int minSampleCountToSend;

    private float lastToggleTime = 0f;
    private float toggleCooldown = 0.2f; // 防止按鍵抖動
    private bool isZToggledOn = false;   // 紀錄 Z 鍵的開關狀態

    void Start()
    {
        // 計算最小發送樣本數
        minSampleCountToSend = Mathf.CeilToInt(sampleRate * sendInterval);

        if (Microphone.devices.Length > 0)
        {
            deviceName = Microphone.devices[0];
            Debug.Log($"<color=green>[AudioSender] 找到麥克風: {deviceName}</color>");
        }
        else
        {
            Debug.LogError("[AudioSender] ❌ 找不到任何麥克風裝置！");
        }
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        float now = Time.time;

        // --- 1. 處理 Z 鍵 (切換基礎狀態) ---
        if (Keyboard.current.zKey.wasPressedThisFrame && (now - lastToggleTime > toggleCooldown))
        {
            isZToggledOn = !isZToggledOn; // 反轉 Z 鍵狀態
            lastToggleTime = now;

            if (isZToggledOn)
            {
                StartRecording();
                Debug.Log("<color=white>[AudioSender] Z 鍵：切換為【開啟】</color>");
            }
            else
            {
                // 如果 Z 關掉時，空白鍵也沒按住，才真正關閉錄音
                if (!Keyboard.current.spaceKey.isPressed)
                {
                    StopRecording();
                }
                Debug.Log("<color=white>[AudioSender] Z 鍵：切換為【關閉】</color>");
            }
        }

        // --- 2. 處理空白鍵 (PTT 按住發話) ---
        if (Keyboard.current.spaceKey.wasPressedThisFrame && (now - lastToggleTime > toggleCooldown))
        {
            // 如果目前還沒在傳輸，就開啟
            if (!isTransmitting) StartRecording();
            lastToggleTime = now;
        }
        else if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            // ★ 防衝突關鍵：只有在 Z 鍵也是關閉的狀態下，放開空白鍵才會停止錄音
            if (!isZToggledOn && isTransmitting)
            {
                StopRecording();
            }
            lastToggleTime = now;
        }

        // --- 3. 錄音處理邏輯 ---
        if (isTransmitting && Microphone.IsRecording(deviceName))
        {
            ProcessAudio();
        }
    }

    // --- 核心功能區 ---

    void StartRecording()
    {
        if (string.IsNullOrEmpty(deviceName)) return;
        if (isTransmitting) return; // 避免重複啟動

        // 通知 Server 開始音訊流 (符合 API 規範)
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.Send("StartAudio", "");

        isTransmitting = true;
        lastSamplePosition = 0;

        // 啟動 10 秒循環錄音
        micClip = Microphone.Start(deviceName, true, 10, sampleRate);
        Debug.Log("<color=cyan>[AudioSender] 🎙️ 麥克風已啟動</color>");
    }

    void StopRecording()
    {
        if (!isTransmitting) return; // 避免重複關閉

        isTransmitting = false;
        Microphone.End(deviceName);

        // 通知 Server 結束音訊流 (符合 API 規範)
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.Send("EndAudio", "");

        Debug.Log("<color=orange>[AudioSender] 🛑 麥克風已關閉</color>");
    }

    void ProcessAudio()
    {
        int currentPosition = Microphone.GetPosition(deviceName);
        int availableSamples = 0;

        if (currentPosition >= lastSamplePosition)
            availableSamples = currentPosition - lastSamplePosition;
        else
            availableSamples = (micClip.samples - lastSamplePosition) + currentPosition;

        // 累積足夠長度才送出
        if (availableSamples < minSampleCountToSend) return;

        float[] samples = new float[availableSamples];

        if (currentPosition >= lastSamplePosition)
        {
            micClip.GetData(samples, lastSamplePosition);
        }
        else
        {
            float[] tail = new float[micClip.samples - lastSamplePosition];
            micClip.GetData(tail, lastSamplePosition);
            float[] head = new float[currentPosition];
            micClip.GetData(head, 0);

            Array.Copy(tail, 0, samples, 0, tail.Length);
            Array.Copy(head, 0, samples, tail.Length, head.Length);
        }

        SendSamples(samples);
        lastSamplePosition = currentPosition;
    }

    void SendSamples(float[] samples)
    {
        // ★ 修正處：放寬檢查邏輯。只有在確定「人在房間管理器但 ID 是空的」時才擋掉。
        // 這樣可以避免 RoomManager 還沒初始化完畢時導致的發送失敗。
        if (RoomManager.Instance != null)
        {
            if (string.IsNullOrEmpty(RoomManager.Instance.CurrentRoomId))
            {
                if (showDebugLog) Debug.LogWarning("[AudioSender] 不在房間內，取消發送音訊");
                return;
            }
        }

        byte[] pcmData = ConvertFloatToByte(samples);
        string base64Str = Convert.ToBase64String(pcmData);

        // ★ 請確認你的 AudioData 定義：
        // 依照 API 文件，欄位名稱必須是 "pcm"
        AudioData payload = new AudioData { pcm = base64Str };

        if (NetworkManager.Instance != null)
        {
            // 發送事件名為 "Audio"
            NetworkManager.Instance.Send<AudioData>("Audio", payload);

            if (showDebugLog)
                Debug.Log($"[AudioSender] 已送出音訊：{samples.Length} 樣本 (Base64 長度: {base64Str.Length})");
        }
    }

    private byte[] ConvertFloatToByte(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i] * micVolume;

            // 防止爆音
            if (s > 1.0f) s = 1.0f;
            if (s < -1.0f) s = -1.0f;

            short shortSample = (short)(s * 32767);
            byte[] bitBytes = BitConverter.GetBytes(shortSample);
            bytes[i * 2] = bitBytes[0];
            bytes[i * 2 + 1] = bitBytes[1];
        }
        return bytes;
    }
}