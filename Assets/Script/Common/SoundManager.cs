using System;
using DG.Tweening;
using UniRx;
using UnityEngine;

/// <summary>
/// 効果音と BGM を鳴らす。シーンに置かなくても、起動時に自動で作られる。
/// 鳴らすときは static の SoundManager.SE / SoundManager.BGM を呼ぶ（Inst がなければ何もしない）。
/// 音の設定は SO_SoundData。音素材が未設定の項目は黙って鳴らさない。
/// 音量とミュートは PlayerPrefs に保存する。
/// 時間を止める演出（Time.timeScale = 0）の間も鳴るよう、時間は unscaled で数える。
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Inst { get; private set; }

    const int SourceCount = 16;
    const int MaxSimultaneousSE = 12;
    const float BGMFadeDuration = 0.6f;

    const string KeyMaster = "sound_master";
    const string KeyBGM = "sound_bgm";
    const string KeySE = "sound_se";
    const string KeyMute = "sound_mute";

    /// <summary>0〜1。</summary>
    public float VolumeMaster { get; private set; } = 1f;
    public float VolumeBGM { get; private set; } = 0.8f;
    public float VolumeSE { get; private set; } = 0.8f;
    public bool Muted { get; private set; }

    /// <summary>設定画面の表示更新用。</summary>
    public event Action SettingsChanged;

    // SE は鳴らし終わったソースを使い回す。ソースごとに、何の音をいつ鳴らし始めたかを覚える。
    AudioSource[] seSources;
    SEType[] seTypes;
    float[] seStarted;
    float[] seBaseVolume;
    readonly System.Collections.Generic.Dictionary<SEType, float> lastPlayed = new();

    // BGM は 2 つのソースを入れ替えてクロスフェードする。
    AudioSource[] bgmSources;
    int bgmFront;
    BGMType currentBGM = BGMType.None;
    float currentBGMVolume = 1f;
    bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        Ensure();
    }

    public static SoundManager Ensure()
    {
        if (Inst != null) return Inst;
        var found = FindFirstObjectByType<SoundManager>();
        if (found != null) return found;
        var go = new GameObject("===SoundManager");
        return go.AddComponent<SoundManager>();
    }

    void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(gameObject);
            return;
        }
        Inst = this;
        DontDestroyOnLoad(gameObject);

        seSources = new AudioSource[SourceCount];
        seTypes = new SEType[SourceCount];
        seStarted = new float[SourceCount];
        seBaseVolume = new float[SourceCount];
        for (int i = 0; i < SourceCount; i++)
            seSources[i] = CreateSource(false);

        bgmSources = new[] { CreateSource(true), CreateSource(true) };
        LoadSettings();

        // シーンのオブジェクトより先に作られるので、最初の状態通知（Title）から受け取れる。
        GameEvent.GameState.SetGameState
            .Subscribe(OnGameState)
            .AddTo(this);
        GameEvent.Stage.Cleared
            .Subscribe(_ => SE(SEType.StageClear))
            .AddTo(this);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    /// <summary>ゲームの状態に合わせて BGM を切り替える。</summary>
    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.Title:
                PlayBGM(BGMType.Title);
                break;
            case GameStateType.OutGame:
                PlayBGM(BGMType.OutGame);
                break;
            case GameStateType.InGame_Ready:
                PlayBGM(BGMType.InGame);
                break;
            case GameStateType.Result:
                PlayBGM(BGMType.Result);
                break;
        }
    }

    #region -- 呼び出し口 --
    /// <summary>効果音を鳴らす。volumeScale は呼び出し側での強弱、pitchScale は音の高さの倍率。</summary>
    public static void SE(SEType type, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (Inst != null) Inst.PlaySE(type, volumeScale, pitchScale);
    }

    /// <summary>BGM を切り替える。同じ曲なら何もしない。</summary>
    public static void BGM(BGMType type)
    {
        if (Inst != null) Inst.PlayBGM(type);
    }
    #endregion

    #region -- SE --
    public void PlaySE(SEType type, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (type == SEType.None || Muted) return;
        var data = SOLoader.SoundData;
        var setting = data != null ? data.GetSE(type) : null;
        if (setting == null) return;
        var clip = setting.PickClip();
        if (clip == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(type, out var last) && now - last < setting.minInterval) return;

        int index = FindSESource(type, Mathf.Max(1, setting.maxVoices));
        if (index < 0) return;
        lastPlayed[type] = now;

        var source = seSources[index];
        source.Stop();
        source.clip = clip;
        seTypes[index] = type;
        seStarted[index] = now;
        seBaseVolume[index] = setting.volume * Mathf.Max(0f, volumeScale);
        source.volume = seBaseVolume[index] * VolumeMaster * VolumeSE;
        float lo = Mathf.Min(setting.pitch.x, setting.pitch.y);
        float hi = Mathf.Max(setting.pitch.x, setting.pitch.y);
        source.pitch = UnityEngine.Random.Range(lo, hi) * pitchScale;
        source.Play();
    }

    /// <summary>
    /// 鳴らすソースを選ぶ。同じ音が maxVoices 鳴っていれば、そのうち一番古いものを止めて使う。
    /// 全体で MaxSimultaneousSE 鳴っていれば、全体で一番古いものを使う。
    /// </summary>
    int FindSESource(SEType type, int maxVoices)
    {
        int free = -1;
        int sameCount = 0;
        int sameOldest = -1;
        int playing = 0;
        int oldest = -1;
        for (int i = 0; i < seSources.Length; i++)
        {
            if (!seSources[i].isPlaying)
            {
                if (free < 0) free = i;
                continue;
            }
            playing++;
            if (oldest < 0 || seStarted[i] < seStarted[oldest]) oldest = i;
            if (seTypes[i] != type) continue;
            sameCount++;
            if (sameOldest < 0 || seStarted[i] < seStarted[sameOldest]) sameOldest = i;
        }

        if (sameCount >= maxVoices) return sameOldest;
        if (playing >= MaxSimultaneousSE || free < 0) return oldest;
        return free;
    }

    public void StopAllSE()
    {
        foreach (var source in seSources)
        {
            source.Stop();
            source.clip = null;
        }
    }
    #endregion

    #region -- BGM --
    public void PlayBGM(BGMType type, float fadeDuration = BGMFadeDuration)
    {
        if (type == currentBGM) return;
        var data = SOLoader.SoundData;
        var setting = data != null ? data.GetBGM(type) : null;
        if (setting == null || setting.clip == null)
        {
            // 曲が未設定なら、今の曲を止めて無音にする。
            StopBGM(fadeDuration);
            currentBGM = type;
            return;
        }

        currentBGM = type;
        currentBGMVolume = setting.volume;

        var outgoing = bgmSources[bgmFront];
        bgmFront = 1 - bgmFront;
        var incoming = bgmSources[bgmFront];

        outgoing.DOKill();
        if (outgoing.isPlaying)
            outgoing.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(outgoing.Stop);

        incoming.DOKill();
        incoming.clip = setting.clip;
        incoming.volume = 0f;
        incoming.Play();
        if (paused) incoming.Pause();
        incoming.DOFade(BGMTargetVolume(), fadeDuration).SetUpdate(true);
    }

    public void StopBGM(float fadeDuration = BGMFadeDuration)
    {
        currentBGM = BGMType.None;
        foreach (var source in bgmSources)
        {
            source.DOKill();
            if (!source.isPlaying) continue;
            var s = source;
            s.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(s.Stop);
        }
    }

    float BGMTargetVolume()
    {
        if (Muted) return 0f;
        return currentBGMVolume * VolumeMaster * VolumeBGM;
    }

    /// <summary>ポーズ画面などで BGM を一時停止する。</summary>
    public void SetPaused(bool value)
    {
        paused = value;
        var front = bgmSources[bgmFront];
        if (value) front.Pause();
        else front.UnPause();
    }
    #endregion

    #region -- 設定 --
    public void SetVolumeMaster(float value) { VolumeMaster = Mathf.Clamp01(value); ApplySettings(); }
    public void SetVolumeBGM(float value) { VolumeBGM = Mathf.Clamp01(value); ApplySettings(); }
    public void SetVolumeSE(float value) { VolumeSE = Mathf.Clamp01(value); ApplySettings(); }
    public void SetMuted(bool value) { Muted = value; ApplySettings(); }

    void LoadSettings()
    {
        VolumeMaster = PlayerPrefs.GetFloat(KeyMaster, 1f);
        VolumeBGM = PlayerPrefs.GetFloat(KeyBGM, 0.8f);
        VolumeSE = PlayerPrefs.GetFloat(KeySE, 0.8f);
        Muted = PlayerPrefs.GetInt(KeyMute, 0) != 0;
    }

    void ApplySettings()
    {
        PlayerPrefs.SetFloat(KeyMaster, VolumeMaster);
        PlayerPrefs.SetFloat(KeyBGM, VolumeBGM);
        PlayerPrefs.SetFloat(KeySE, VolumeSE);
        PlayerPrefs.SetInt(KeyMute, Muted ? 1 : 0);
        PlayerPrefs.Save();

        // フェード中でなければ、今の BGM の音量をすぐ変える。
        var front = bgmSources[bgmFront];
        if (!DOTween.IsTweening(front))
            front.volume = BGMTargetVolume();

        float seScale = Muted ? 0f : VolumeMaster * VolumeSE;
        for (int i = 0; i < seSources.Length; i++)
        {
            if (seSources[i].isPlaying)
                seSources[i].volume = seBaseVolume[i] * seScale;
        }

        SettingsChanged?.Invoke();
    }
    #endregion
}
