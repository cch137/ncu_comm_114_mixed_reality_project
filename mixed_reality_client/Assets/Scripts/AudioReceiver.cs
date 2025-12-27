using UnityEngine;
using System;
using System.Collections.Concurrent; // 記得引用這個！用來做執行緒安全的佇列

[RequireComponent(typeof(AudioSource))]
public class AudioReceiver : MonoBehaviour
{
    private AudioSource audioSource;

    // 聲音緩衝區 (先進先出)
    private ConcurrentQueue<float> audioQueue = new ConcurrentQueue<float>();

    // 為了讓 OnAudioFilterRead 運作，AudioSource 需要播放一個「空的」Clip
    // 這樣引擎才會一直運轉，然後我們再動態把聲音塞進去
    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived += HandleAudioData;
        }

        // ★ 關鍵技巧：播放一個靜音的 Loop，讓 Audio Engine 保持啟動
        // 這樣 OnAudioFilterRead 才會被持續呼叫
        audioSource.loop = true;
        audioSource.clip = AudioClip.Create("Dummy", 16000, 1, 16000, false);
        audioSource.Play();
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnAudioReceived -= HandleAudioData;
        }
    }

    // 1. 接收端：收到資料，轉好格式，塞進排隊隊伍 (Queue)
    void HandleAudioData(AudioData data)
    {
        if (string.IsNullOrEmpty(data.pcm)) return;

        byte[] bytes = Convert.FromBase64String(data.pcm);
        float[] newSamples = ConvertByteToFloat(bytes);

        // 把每一個 float 樣本都塞進佇列
        foreach (float sample in newSamples)
        {
            audioQueue.Enqueue(sample);
        }
    }

    // 2. 播放端：Unity 引擎每隔幾毫秒會自動呼叫這個函式要資料
    // data: 引擎給你的空陣列，你要負責填滿它
    // channels: 聲道數
    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            // 從佇列拿一個樣本
            if (audioQueue.TryDequeue(out float sample))
            {
                // 如果是單聲道，就填入所有聲道 (通常 VR 裡 data 是雙聲道)
                data[i] = sample;

                // 如果是雙聲道 (Stereo)，右耳也要填一樣的值 (不然會只有左耳有聲音)
                if (channels > 1) data[i + 1] = sample;
            }
            else
            {
                // 如果佇列空了 (網路卡頓)，就填 0 (靜音)，不然會有爆音
                data[i] = 0;
                if (channels > 1) data[i + 1] = 0;
            }
        }
    }

    // 轉檔邏輯保持不變
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