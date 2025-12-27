using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class AudioSender : MonoBehaviour
{
    [Header("設定")]
    // 設定錄音頻率 (必須跟 Server / 接收端一致)
    public int sampleRate = 16000;
    public bool showDebugLog = true; // 開關 Log 避免洗頻

    // 錄音用的 Clip
    private AudioClip micClip;
    private string deviceName;

    // 紀錄上次讀取到的位置
    private int lastSamplePosition = 0;

    // 是否正在發話
    public bool isTransmitting = false;

    void Start()
    {
        // 1. 取得麥克風裝置 (預設第一個)
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
        // 確保鍵盤存在
        if (Keyboard.current == null) return;

        // ★修正邏輯：按住空白鍵說話 (Push-to-Talk)

        // 1. 按下瞬間 -> 開始
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!isTransmitting) StartRecording();
        }

        // 2. 放開瞬間 -> 停止
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            if (isTransmitting) StopRecording();
        }

        // 如果正在錄音，就持續擷取並發送
        if (isTransmitting && Microphone.IsRecording(deviceName))
        {
            SendAudioChunk();
        }
    }

    void StartRecording()
    {
        if (string.IsNullOrEmpty(deviceName)) return;

        isTransmitting = true;
        lastSamplePosition = 0;

        // 開始錄音：10秒循環 (足夠緩衝用)
        micClip = Microphone.Start(deviceName, true, 10, sampleRate);

        Debug.Log("<color=cyan>[AudioSender] 🎙️ 開始錄音 (按住 Space)...</color>");
    }

    void StopRecording()
    {
        isTransmitting = false;
        Microphone.End(deviceName);

        Debug.Log("<color=orange>[AudioSender] 🛑 停止錄音</color>");
    }

    void SendAudioChunk()
    {
        // 取得目前麥克風寫入到的位置
        int currentPosition = Microphone.GetPosition(deviceName);

        // 如果位置沒變，代表沒有新聲音
        if (currentPosition == lastSamplePosition) return;

        // 計算要抓取多少樣本
        int sampleCount = 0;

        // 正常情況
        if (currentPosition > lastSamplePosition)
        {
            sampleCount = currentPosition - lastSamplePosition;
        }
        // 繞圈情況 (Loop Wrap-around)
        else
        {
            // 為了簡化，繞圈時重置指標，會掉一小幀但不影響通話
            lastSamplePosition = currentPosition;
            return;
        }

        // 1. 從 AudioClip 抓取 Float 資料
        float[] samples = new float[sampleCount];
        micClip.GetData(samples, lastSamplePosition);

        // 2. Float 轉 Byte[] (PCM 16-bit)
        byte[] pcmData = ConvertFloatToByte(samples);

        // 3. Byte[] 轉 Base64
        string base64Str = Convert.ToBase64String(pcmData);

        // 4. 發送
        AudioData payload = new AudioData { pcm = base64Str };

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send<AudioData>("Audio", payload);

            // 顯示 Log (只顯示長度，證明有在送)
            if (showDebugLog)
                Debug.Log($"[AudioSender] 發送封包 Size: {pcmData.Length} bytes");
        }
        else
        {
            Debug.LogWarning("[AudioSender] ❌ NetworkManager 不存在，無法發送！");
        }

        // 更新位置指標
        lastSamplePosition = currentPosition;
    }

    private byte[] ConvertFloatToByte(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        float gain = 1.0f; // 音量增益

        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i] * gain;

            // Clamping
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