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

/// <summary>BGM の 1 曲。場面によらず、一覧からランダムに選んで流し続ける。</summary>
[Serializable]
public class BGMTrack
{
    public string memo;
    public AudioClip clip;
    [Tooltip("曲ごとの音量。曲どうしの大きさの差をそろえる")]
    [Range(0f, 1f)] public float volume = 1f;
}

/// <summary>BGM の場面ごとの音量と曇り。音量は曲の音量に掛ける。</summary>
[Serializable]
public class BGMSetting
{
    public BGMType type;
    public string memo;
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("曇り（ローパスフィルター）。0 で曇らない、1 でいちばんこもる。場面が変わると音量と一緒にゆっくり変わる")]
    [Range(0f, 1f)] public float muffle;

    /// <summary>曇りの量をローパスのカットオフ周波数（Hz）にする。0 は 22000Hz（掛けない）、1 は 400Hz。</summary>
    public float Cutoff => Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(400f), Mathf.Clamp01(muffle)));
}

/// <summary>
/// 効果音と BGM の一覧。Resources/SO/SO_SoundData から読む。
/// 音素材が未設定の項目は鳴らさないだけで、エラーにはしない。
/// </summary>
[CreateAssetMenu(menuName = "SO/SoundData")]
public class SO_SoundData : ScriptableObject
{
    [Tooltip("BGM の曲の一覧。場面によらず、ここからランダムに選んで流し続ける")]
    public BGMTrack[] bgmTracks = Array.Empty<BGMTrack>();
    [Tooltip("場面ごとの BGM の音量。場面が変わっても曲は変えず、音量だけをこの値へ寄せる")]
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

    /// <summary>
    /// 次に流す曲の番号。音素材のある曲からランダムに選ぶ。2 曲以上あれば、今の曲（current）は避ける。
    /// 曲がなければ -1。
    /// </summary>
    public int PickBGMTrack(int current)
    {
        if (bgmTracks == null) return -1;
        var candidates = new List<int>();
        for (int i = 0; i < bgmTracks.Length; i++)
            if (bgmTracks[i] != null && bgmTracks[i].clip != null && i != current) candidates.Add(i);
        if (candidates.Count == 0)
            return current >= 0 && current < bgmTracks.Length && bgmTracks[current]?.clip != null ? current : -1;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
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
            case SEType.Transition:
                s.volume = 0.7f; s.minInterval = 0.3f; s.maxVoices = 1; s.pitch = new Vector2(0.97f, 1.03f);
                break;
            case SEType.GlassHit:
                s.volume = 0.6f; s.minInterval = 0.04f; s.maxVoices = 3; s.pitch = new Vector2(0.92f, 1.1f);
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
