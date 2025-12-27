using System;
using UnityEngine;

// 1. 基礎外殼：只為了偷看 "type" 是什麼
[Serializable]
public class BaseMessage
{
    public string type;
}

// 2. 萬用外殼：確認 type 後，用這個把 payload 轉成正確的類別
[Serializable]
public class MessageWrapper<T>
{
    public string type;
    public T data;
}

[Serializable]
public class HeadPoseData
{
    public float[] pos; // [x, y, z]
    public float[] rot; // [x, y, z, w]
}

[Serializable]
public class HandPoseData
{
    public float[] lpos; public float[] lrot;
    public float[] rpos; public float[] rrot;
}

[Serializable]
public class RoomData
{
    public string id;
}

[Serializable]

public class ErrorMsg
{
    public string errorMessage;
}

[Serializable]
public class RoomErrorData
{
    public string reason;
}

[Serializable]
public class GLTFData
{
    public string id;
    public string name;
    public string url;
}

[Serializable]
public class AudioData
{
    public string pcm; // 這裡收到的是 Base64 編碼的長字串
}

[System.Serializable]
public class GLTFResultData
{
    public string id; // 對應 server 要求的 { id: string }
}