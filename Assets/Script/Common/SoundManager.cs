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
    // 曲が最後まで鳴り終わってから、次の曲を始めるまでの無音（秒）。
    const float TrackGap = 3f;
    // 次の曲の頭のフェードイン（秒）。プツッと鳴らないための短いもの。
    const float TrackFadeIn = 0.1f;

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

    // BGM は場面によらず同じ一覧（SO_SoundData の bgmTracks）からランダムに流し続ける。
    // 曲は最後まで鳴らし切り、TrackGap 秒の無音をはさんで次の曲を始める。場面が変わったときは音量だけ変える。
    // 2 つのソースは、曲が鳴っている途中で止めるとき（StopBGM のあとの再開など）に、前の曲をフェードアウトさせるために使う。
    // 曲の音声は Streaming で読む（Assets/Editor/BgmImportSettings.cs）。読み込み時に全部展開すると、曲を始める瞬間にかくつく。
    AudioSource[] bgmSources;
    float[] trackVolume = { 1f, 1f };
    int bgmFront;
    int currentTrack = -1;
    BGMType currentBGM = BGMType.None;
    float sceneVolume = 1f;
    // 曇り（ローパス）の今のカットオフ周波数。場面が変わると、音量と同じ時間でそこへ寄せる。
    AudioLowPassFilter[] bgmFilters;
    float bgmCutoff = NoMuffleCutoff;
    float sceneCutoff = NoMuffleCutoff;
    // 画面切り替えの幕が閉じている間は、場面の曇りよりさらにこもらせる。
    bool curtainMuffle;
    const float CurtainCutoff = 900f;
    const float NoMuffleCutoff = 22000f;
    const string CutoffTweenId = "SoundManager.BgmCutoff";
    bool paused;
    bool bgmStopped;
    // 今の曲をどこまで聞いたか（秒）。
    float trackHeard;
    // 曲が終わってから次の曲までの残りの無音（秒）。0 未満なら、曲の終わりを待っている。
    float gapLeft = -1f;

    /// <summary>今の BGM の場面（音量を決める）。</summary>
    public BGMType CurrentBGMScene => currentBGM;

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

        // ローパスフィルターは同じ GameObject の音すべてに掛かるので、BGM は子の GameObject に分ける。
        bgmSources = new AudioSource[2];
        bgmFilters = new AudioLowPassFilter[2];
        for (int i = 0; i < 2; i++)
        {
            // 有効なまま部品を足すと、曲のない音源で再生が走って警告が出るので、組み立ててから有効にする。
            var child = new GameObject("BGM_" + i);
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            bgmSources[i] = child.AddComponent<AudioSource>();
            bgmSources[i].playOnAwake = false;
            bgmSources[i].loop = false;
            bgmSources[i].spatialBlend = 0f;
            bgmFilters[i] = child.AddComponent<AudioLowPassFilter>();
            bgmFilters[i].enabled = false;
            child.SetActive(true);
        }
        ApplyCutoff();
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

    /// <summary>ゲームの状態に合わせて BGM の音量を変える（曲はそのまま）。</summary>
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

    /// <summary>BGM の場面を変える。曲はそのままで、場面ごとの音量になる。</summary>
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
    /// <summary>
    /// BGM の場面を変える。曲は変えず、場面ごとの音量（SO_SoundData の bgm）へゆっくり寄せる。
    /// 曲が流れていなければ、一覧からランダムに 1 曲選んで流し始める。
    /// </summary>
    public void PlayBGM(BGMType type, float fadeDuration = BGMFadeDuration)
    {
        var data = SOLoader.SoundData;
        var setting = data != null ? data.GetBGM(type) : null;
        currentBGM = type;
        sceneVolume = setting != null ? setting.volume : 1f;
        bgmStopped = false;
        sceneCutoff = setting != null ? setting.Cutoff : NoMuffleCutoff;
        // ワクチンを撒いたあとのタイトルは、こもりを外して少し明るく聞かせる。
        if (type == BGMType.Title && VaccineRuntime.Cleared)
            sceneCutoff = NoMuffleCutoff;
        FadeCutoff(TargetCutoff(), fadeDuration);

        var front = bgmSources[bgmFront];
        if (currentTrack < 0 || front.clip == null)
        {
            StartNextTrack(fadeDuration);
            return;
        }
        front.DOKill();
        front.DOFade(BGMTargetVolume(bgmFront), fadeDuration).SetUpdate(true);
    }

    /// <summary>画面切り替えの幕が閉じている間、BGM をこもらせる。開くと場面の曇りへ戻る。</summary>
    public void SetCurtainMuffle(bool on, float duration)
    {
        curtainMuffle = on;
        FadeCutoff(TargetCutoff(), duration);
    }

    float TargetCutoff()
    {
        return curtainMuffle ? Mathf.Min(sceneCutoff, CurtainCutoff) : sceneCutoff;
    }

    void FadeCutoff(float target, float duration)
    {
        DOTween.Kill(CutoffTweenId);
        if (duration <= 0f || Mathf.Approximately(bgmCutoff, target))
        {
            bgmCutoff = target;
            ApplyCutoff();
            return;
        }
        // 周波数は耳には対数で聞こえるので、対数のまま動かす。
        float from = Mathf.Log(bgmCutoff);
        float to = Mathf.Log(target);
        DOTween.To(() => 0f, t =>
            {
                bgmCutoff = Mathf.Exp(Mathf.Lerp(from, to, t));
                ApplyCutoff();
            }, 1f, duration)
            .SetUpdate(true)
            .SetId(CutoffTweenId);
    }

    void ApplyCutoff()
    {
        if (bgmFilters == null) return;
        // 曇らせないときはフィルターを切って、処理を省く。
        // 曲の入っていない音源でフィルターだけを有効にすると Unity が警告を出すので、そのときも切る。
        bool on = bgmCutoff < NoMuffleCutoff - 1f;
        for (int i = 0; i < bgmFilters.Length; i++)
        {
            var filter = bgmFilters[i];
            if (filter == null) continue;
            filter.enabled = on && bgmSources[i] != null && bgmSources[i].clip != null;
            filter.cutoffFrequency = bgmCutoff;
        }
    }

    /// <summary>一覧から、今と違う曲をランダムに選んで流し始める。前の曲がまだ鳴っていれば、フェードアウトさせる。</summary>
    void StartNextTrack(float fadeDuration)
    {
        gapLeft = -1f;
        var data = SOLoader.SoundData;
        int next = data != null ? data.PickBGMTrack(currentTrack) : -1;
        var outgoing = bgmSources[bgmFront];
        outgoing.DOKill();
        if (outgoing.isPlaying)
            outgoing.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(outgoing.Stop);
        currentTrack = next;
        if (next < 0) return; // 曲が未設定なら無音。

        var track = data.bgmTracks[next];
        bgmFront = 1 - bgmFront;
        var incoming = bgmSources[bgmFront];
        incoming.DOKill();
        incoming.clip = track.clip;
        trackVolume[bgmFront] = track.volume;
        incoming.volume = 0f;
        incoming.time = 0f;
        trackHeard = 0f;
        ApplyCutoff();
        incoming.Play();
        if (paused) incoming.Pause();
        incoming.DOFade(BGMTargetVolume(bgmFront), fadeDuration).SetUpdate(true);
    }

    /// <summary>曲が最後まで鳴り終わったら、TrackGap 秒の無音のあとに次の曲を始める。</summary>
    void Update()
    {
        if (paused || bgmStopped || currentTrack < 0) return;

        if (gapLeft >= 0f)
        {
            gapLeft -= Time.unscaledDeltaTime;
            if (gapLeft < 0f)
                StartNextTrack(TrackFadeIn);
            return;
        }

        var front = bgmSources[bgmFront];
        var clip = front.clip;
        if (clip == null) return;
        if (front.isPlaying)
        {
            trackHeard = Mathf.Max(trackHeard, front.time);
            return;
        }
        // 止まっているだけ（音が出ない環境やエディタの一時停止）では次へ進めない。
        // 終わりの近くまで聞いたあとに止まったときだけ、曲が鳴り終わったとみなす。
        if (trackHeard >= clip.length - 1f)
            gapLeft = TrackGap;
    }

    public void StopBGM(float fadeDuration = BGMFadeDuration)
    {
        bgmStopped = true;
        currentTrack = -1;
        gapLeft = -1f;
        foreach (var source in bgmSources)
        {
            source.DOKill();
            if (!source.isPlaying) continue;
            var s = source;
            s.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(s.Stop);
        }
    }

    float BGMTargetVolume(int index)
    {
        if (Muted) return 0f;
        return trackVolume[index] * sceneVolume * VolumeMaster * VolumeBGM;
    }

    /// <summary>ポーズ画面などで BGM を一時停止する。</summary>
    public void SetPaused(bool value)
    {
        paused = value;
        foreach (var source in bgmSources)
        {
            if (value) source.Pause();
            else source.UnPause();
        }
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
            front.volume = BGMTargetVolume(bgmFront);

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
