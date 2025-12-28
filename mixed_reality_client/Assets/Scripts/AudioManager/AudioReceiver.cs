using UnityEngine;
using System;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class AudioReceiver : MonoBehaviour
{
    private AudioSource audioSource;

    // 執行緒安全的佇列
    private ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    [Header("音訊設定")]
    [Tooltip("必須與 Server 錄音的取樣率一致 (例如 16000, 44100, 48000)")]
    public int sampleRate = 16000;

    [Tooltip("緩衝多少樣本才開始播放 (防止網路抖動造成的爆音)")]
    // 假設 16000Hz，緩衝 0.1秒 = 1600 個樣本
    public int bufferThreshold = 1600;

    private bool isBuffering = true;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived += HandleAudioData;
        }

        // ★ 關鍵：建立一個對應頻率的空 Clip
        audioSource.loop = true;
        audioSource.clip = AudioClip.Create("VoIP_Stream", sampleRate, 1, sampleRate, false);
        audioSource.Play();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived -= HandleAudioData;
        }
    }

    // 1. 接收資料 (Main Thread 或 Network Thread)
    void HandleAudioData(AudioData data)
    {
        if (string.IsNullOrEmpty(data.pcm)) return;

        byte[] bytes = Convert.FromBase64String(data.pcm);
        float[] newSamples = ConvertByteToFloat(bytes);

        foreach (float sample in newSamples)
        {
            audioQueue.Enqueue(sample);
        }
    }

    // 2. 播放邏輯 (Audio Thread - 極高頻率呼叫)
    void OnAudioFilterRead(float[] data, int channels)
    {
        // 如果正在緩衝狀態，檢查是否累積足夠資料
        if (isBuffering)
        {
            if (audioQueue.Count >= bufferThreshold)
            {
                isBuffering = false; // 累積夠了，開始播放
            }
            else
            {
                // 還沒夠，先填靜音
                Array.Clear(data, 0, data.Length);
                return;
            }
        }

        // 開始填入資料
        for (int i = 0; i < data.Length; i += channels)
        {
            if (audioQueue.TryDequeue(out float sample))
            {
                data[i] = sample;

                // 處理多聲道 (複製單聲道資料到所有聲道)
                if (channels > 1)
                {
                    for (int c = 1; c < channels; c++)
                    {
                        data[i + c] = sample;
                    }
                }
            }
            else
            {
                // 資料用盡 (Buffer Underrun)
                // 填入靜音並重新進入緩衝狀態，避免斷斷續續的雜音
                data[i] = 0;
                if (channels > 1) data[i + 1] = 0;

                isBuffering = true;
            }
        }
    }

    private float[] ConvertByteToFloat(byte[] array)
    {
        // 假設是 16-bit PCM (Short)
        float[] floatArr = new float[array.Length / 2];
        for (int i = 0; i < floatArr.Length; i++)
        {
            short audioSample = BitConverter.ToInt16(array, i * 2);
            floatArr[i] = audioSample / 32768.0f;
        }
        return floatArr;
    }
}