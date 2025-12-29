using UnityEngine;
using System;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class AudioReceiver : MonoBehaviour
{
    private AudioSource audioSource;
    private ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    [Header("音訊設定")]
    public int sampleRate = 16000;
    public int bufferThreshold = 1600;

    // 用來控制是否正在播放，或是在等待資料累積
    private volatile bool isBuffering = true;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived += HandleAudioData;
            // ★ 訂閱 Flush 事件
            NetworkManager.Instance.OnFlushAudio += HandleFlushAudio;
        }

        audioSource.loop = true;
        audioSource.clip = AudioClip.Create("VoIP_Stream", sampleRate, 1, sampleRate, false);
        audioSource.Play();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived -= HandleAudioData;
            // ★ 取消訂閱
            NetworkManager.Instance.OnFlushAudio -= HandleFlushAudio;
        }
    }

    // ★★★ 新增：處理中斷訊號 ★★★
    void HandleFlushAudio()
    {
        // 1. 清空所有累積的舊音訊
        // ConcurrentQueue 在舊版 .NET 沒有 Clear()，用 TryDequeue 迴圈清空最保險
        while (audioQueue.TryDequeue(out _)) { }

        // 2. 進入緩衝狀態 (這會導致 OnAudioFilterRead 填入靜音，直到新資料來)
        isBuffering = true;

        Debug.Log("[AudioReceiver] 已清空緩衝佇列，暫停播放等待新資料...");
    }

    void HandleAudioData(AudioData data)
    {
        // 如果 Server 送來的資料是空的，直接忽略
        if (string.IsNullOrEmpty(data.pcm)) return;

        byte[] bytes = Convert.FromBase64String(data.pcm);
        float[] newSamples = ConvertByteToFloat(bytes);

        // 將新資料放入 Queue
        foreach (float sample in newSamples)
        {
            audioQueue.Enqueue(sample);
        }
    }

    // 音訊執行緒 (高頻呼叫)
    void OnAudioFilterRead(float[] data, int channels)
    {
        // 如果正在緩衝狀態 (剛開始 或 剛被 Flush)
        if (isBuffering)
        {
            // 檢查累積的資料夠不夠
            if (audioQueue.Count >= bufferThreshold)
            {
                isBuffering = false; // 資料夠了，開始播放
            }
            else
            {
                // 資料不夠，輸出靜音
                Array.Clear(data, 0, data.Length);
                return;
            }
        }

        // 正常播放邏輯
        for (int i = 0; i < data.Length; i += channels)
        {
            if (audioQueue.TryDequeue(out float sample))
            {
                data[i] = sample;
                if (channels > 1)
                {
                    for (int c = 1; c < channels; c++) data[i + c] = sample;
                }
            }
            else
            {
                // ★ Buffer Underrun (播到沒資料了)
                // 這種情況也視為一種「小斷線」，填靜音並重新緩衝
                data[i] = 0;
                if (channels > 1) data[i + 1] = 0;
                isBuffering = true;
            }
        }
    }

    private float[] ConvertByteToFloat(byte[] array)
    {
        float[] floatArr = new float[array.Length / 2];
        for (int i = 0; i < floatArr.Length; i++)
        {
            short audioSample = BitConverter.ToInt16(array, i * 2);
            floatArr[i] = audioSample / 32768.0f;
        }
        return floatArr;
    }
}