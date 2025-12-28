using System;
using UnityEngine;

// ==========================================
//   PART 1: 核心使用的類別 (Used in NetworkManager)
// ==========================================

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

// 基礎資料結構 (被其他類別引用)
[Serializable]
public class PoseData
{
    public float[] pos;
    public float[] rot;
}

[Serializable]
public class GLTFInfo
{
    public string name;
    public string url;
}

// --- 實體相關 Payload ---

// 專用於 CreateEntityProgObj
[Serializable]
public class CreateProgObjData
{
    public string id;
    public PoseData pose;
    public GLTFInfo gltf;
}

// 用於 CreateEntityGeomObj, CreateEntityAnchor 與 UpdateEntity
[Serializable]
public class EntityBaseData
{
    public string id;
    public PoseData pose;
}

[Serializable]
public class DeleteEntityData
{
    public string id;
}

// --- 系統/房間/音訊 Payload ---

[Serializable]
public class RoomData
{
    public string id;
}

[Serializable]
public class AudioData
{
    public string pcm; // Base64 encoded buffer
}

[Serializable]
public class ErrorMsg
{
    public string message;
}

// [Image 3] 用於 ClaimEntity 與 ReleaseEntity 的 Payload
[Serializable]
public class EntityControlData
{
    public string id;
}

// [Image 2] 玩家同步全身資訊 (頭、左手、右手)
// 這裡不需要額外定義 Class，因為 Poses 是一個 PoseData[] 陣列
// 但我們可以定義一個常數來幫助記憶索引
public static class BodyIndex
{
    public const int HEAD = 0;
    public const int LEFT_HAND = 1;
    public const int RIGHT_HAND = 2;
}


// ==========================================
//   PART 2: 目前未被使用 / 舊版 / 預留類別
//   (Not currently referenced in HandleMessage)
// ==========================================

[Serializable]
public class RoomErrorData
{
    public string reason;
}

[Serializable]
public class HeadPoseData
{
    public float[] pos;
    public float[] rot;
}

[Serializable]
public class HandPoseData
{
    public float[] lpos; public float[] lrot;
    public float[] rpos; public float[] rrot;
}

[Serializable]
public class GLTFData
{
    public string id;
    public string name;
    public string url;
}

[System.Serializable]
public class GLTFResultData
{
    public string id;
}

[Serializable]
public class EntityData
{
    public string id;
    public string type;
    public PoseData pose;
}