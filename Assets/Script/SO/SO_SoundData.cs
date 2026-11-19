using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>効果音 1 種類分の設定。</summary>
[Serializable]
public class SESetting
{
    public SEType type;
    public string memo;
    [Tooltip("複数入れると、鳴らすたびにランダムで選ぶ")]
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("ピッチのランダム幅（連続で鳴らしたときの単調さを減らす）")]
    public Vector2 pitch = new Vector2(0.95f, 1.05f);
    [Tooltip("同じ音を続けて鳴らせる最短の間隔（秒）。これより短い間隔の再生は捨てる")]
    public float minInterval = 0.05f;
    [Tooltip("同じ音を同時に鳴らせる数。超えたときは一番古いものを止めて鳴らす")]
    public int maxVoices = 3;

    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }
}

/// <summary>BGM 1 曲分の設定。</summary>
[Serializable]
public class BGMSetting
{
    public BGMType type;
    public string memo;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.7f;
}

/// <summary>
/// 効果音と BGM の一覧。Resources/SO/SO_SoundData から読む。
/// 音素材が未設定の項目は鳴らさないだけで、エラーにはしない。
/// </summary>
[CreateAssetMenu(menuName = "SO/SoundData")]
public class SO_SoundData : ScriptableObject
{
    public BGMSetting[] bgm = Array.Empty<BGMSetting>();
    public SESetting[] se = Array.Empty<SESetting>();

    Dictionary<SEType, SESetting> seMap;
    Dictionary<BGMType, BGMSetting> bgmMap;

    public SESetting GetSE(SEType type)
    {
        if (seMap == null)
        {
            seMap = new Dictionary<SEType, SESetting>();
            foreach (var s in se)
                if (s != null) seMap[s.type] = s;
        }
        seMap.TryGetValue(type, out var setting);
        return setting;
    }

    public BGMSetting GetBGM(BGMType type)
    {
        if (bgmMap == null)
        {
            bgmMap = new Dictionary<BGMType, BGMSetting>();
            foreach (var b in bgm)
                if (b != null) bgmMap[b.type] = b;
        }
        bgmMap.TryGetValue(type, out var setting);
        return setting;
    }

    void OnValidate()
    {
        seMap = null;
        bgmMap = null;
    }

    /// <summary>enum にあって一覧にない項目を、種類ごとの初期値で足す。</summary>
    [ContextMenu("足りない項目を追加")]
    public void AddMissingEntries()
    {
        var seList = new List<SESetting>(se ?? Array.Empty<SESetting>());
        foreach (SEType type in Enum.GetValues(typeof(SEType)))
        {
            if (type == SEType.None || seList.Exists(s => s != null && s.type == type)) continue;
            seList.Add(DefaultSE(type));
        }
        seList.Sort((a, b) => ((int)a.type).CompareTo((int)b.type));
        se = seList.ToArray();

        var bgmList = new List<BGMSetting>(bgm ?? Array.Empty<BGMSetting>());
        foreach (BGMType type in Enum.GetValues(typeof(BGMType)))
        {
            if (type == BGMType.None || bgmList.Exists(b => b != null && b.type == type)) continue;
            bgmList.Add(new BGMSetting { type = type, memo = type.ToString() });
        }
        bgmList.Sort((a, b) => ((int)a.type).CompareTo((int)b.type));
        bgm = bgmList.ToArray();

        OnValidate();
    }

    /// <summary>種類ごとの初期値。頻繁に鳴る音ほど間隔を空け、同時数を絞る。</summary>
    static SESetting DefaultSE(SEType type)
    {
        var s = new SESetting { type = type, memo = type.ToString() };
        switch (type)
        {
            case SEType.UI_Hover:
                s.volume = 0.5f; s.minInterval = 0.04f; s.maxVoices = 1; s.pitch = Vector2.one;
                break;
            case SEType.UI_Click:
            case SEType.UI_Cancel:
            case SEType.UI_PopupOpen:
            case SEType.UI_Error:
                s.minInterval = 0.04f; s.maxVoices = 2; s.pitch = Vector2.one;
                break;
            case SEType.UI_Purchase:
            case SEType.UI_PurchaseMax:
                s.minInterval = 0.03f; s.maxVoices = 3; s.pitch = new Vector2(0.98f, 1.02f);
                break;
            case SEType.BallLaunch:
                s.volume = 0.6f; s.minInterval = 0.04f; s.maxVoices = 3;
                break;
            case SEType.BlockHit:
                s.volume = 0.5f; s.minInterval = 0.03f; s.maxVoices = 4; s.pitch = new Vector2(0.9f, 1.1f);
                break;
            case SEType.BlockHitCritical:
                s.volume = 0.7f; s.minInterval = 0.05f; s.maxVoices = 2;
                break;
            case SEType.BlockBreak:
                s.volume = 0.8f; s.minInterval = 0.04f; s.maxVoices = 4; s.pitch = new Vector2(0.9f, 1.1f);
                break;
            case SEType.ArmorDeflect:
                s.volume = 0.7f; s.minInterval = 0.05f; s.maxVoices = 2; s.pitch = new Vector2(0.92f, 1.08f);
                break;
            case SEType.RegenHeal:
                s.volume = 0.6f; s.minInterval = 0.1f; s.maxVoices = 2;
                break;
            case SEType.Explosion:
            case SEType.Laser:
            case SEType.Poison:
                s.volume = 0.7f; s.minInterval = 0.06f; s.maxVoices = 2;
                break;
            case SEType.ResourceCollect:
                s.volume = 0.5f; s.minInterval = 0.03f; s.maxVoices = 3; s.pitch = new Vector2(0.95f, 1.15f);
                break;
            case SEType.BossSpawn:
            case SEType.BossDefeat:
            case SEType.StageClear:
            case SEType.StageFail:
                s.minInterval = 0.5f; s.maxVoices = 1; s.pitch = Vector2.one;
                break;
        }
        return s;
    }
}
