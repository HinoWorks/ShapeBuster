using UnityEngine;
using System;
using System.Collections;
using DG.Tweening;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Inst;

    [Range(0, 100)]
    public float Volume_Master = 100f;
    [Range(0, 100)]
    public float Volume_BGM = 80f;
    [Range(0, 100)]
    public float Volume_Sound = 80f;
    public bool Mute_Master;
    public bool Mute_BGM;
    public bool Mute_SE;

    private const int MAX_SIMULTANEOUS_SE = 8;
    private const float BGM_FADE_DURATION = 0.5f;
    [Tooltip("SE再生時のピッチランダム範囲（連続再生の違和感軽減）")]
    [SerializeField] private float sePitchMin = 0.92f;
    [SerializeField] private float sePitchMax = 1.08f;

    // === AudioSource ===
    private AudioSource BGMsource;
    private AudioSource[] SEsources = new AudioSource[16];
    private SO_SoundElement soundData_BGM;
    private int currentBGMIndex = -1;

    // -- SE同時になってしまう現状回避 --
    private float timer;
    private float duration_SE = 0.2f;
    private bool isSEPlayed = false;

    private bool isPaused;

    void Awake()
    {
        if (Inst == null)
        {
            Inst = this;
            DontDestroyOnLoad(this.gameObject);
            EnsureUserSettingsManager();
        }
        else
        {
            Destroy(this);
            return;
        }

        // BGM AudioSource
        BGMsource = gameObject.AddComponent<AudioSource>();
        BGMsource.loop = true;

        // SE AudioSource
        for (int i = 0; i < SEsources.Length; i++)
        {
            SEsources[i] = gameObject.AddComponent<AudioSource>();
        }

        ApplyVolumeAndMute();
    }

    void Start()
    {
        //int index = UserSettingsManager.Inst != null ? UserSettingsManager.Inst.BgmIndex : 0;
        int index = 0;
        PlayBGM(index);
    }

    void Update()
    {
        if (isSEPlayed)
        {
            timer += Time.deltaTime;
            if (timer >= duration_SE)
            {
                isSEPlayed = false;
                timer = 0;
            }
        }
    }

    #region -- Settings --
    private static void EnsureUserSettingsManager()
    {
        /*
        if (UserSettingsManager.Inst != null) return;
        if (FindFirstObjectByType<UserSettingsManager>() != null) return;
        var go = new GameObject("===UserSettingsManager");
        go.AddComponent<UserSettingsManager>();
    */
    }

    /// <summary>UserSettingsManager から設定を反映する</summary>
    public void ApplySoundSettings(
        float volumeMaster, float volumeBGM, float volumeSE,
        bool muteMaster, bool muteBGM, bool muteSE,
        int bgmIndex)
    {
        Volume_Master = volumeMaster;
        Volume_BGM = volumeBGM;
        Volume_Sound = volumeSE;
        Mute_Master = muteMaster;
        Mute_BGM = muteBGM;
        Mute_SE = muteSE;
        ApplyVolumeAndMute();
        PlayBGMWithFade(bgmIndex);
    }

    private float GetBGMVolumeMultiplier()
    {
        if (Mute_Master || Mute_BGM) return 0f;
        return (Volume_Master / 100f) * (Volume_BGM / 100f);
    }

    private float GetSEVolumeMultiplier()
    {
        if (Mute_Master || Mute_SE) return 0f;
        return (Volume_Master / 100f) * (Volume_Sound / 100f);
    }

    private void ApplyVolumeAndMute()
    {
        if (BGMsource == null) return;

        BGMsource.mute = Mute_Master || Mute_BGM || isPaused;
        float bgmBaseVolume = (soundData_BGM != null) ? soundData_BGM.Volume : 1f;
        BGMsource.volume = (Mute_Master || Mute_BGM || isPaused) ? 0f : bgmBaseVolume * GetBGMVolumeMultiplier();

        for (int i = 0; i < SEsources.Length; i++)
        {
            if (SEsources[i] != null)
            {
                SEsources[i].mute = Mute_Master || Mute_SE;
            }
        }
    }

    /// <summary>音量・ミュート変更時に呼ぶ（設定UIからは UserSettingsManager 経由を推奨）</summary>
    public void SetVolumeMaster(float value)
    {

        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetVolumeMaster(value);
    }

    public void SetVolumeBGM(float value)
    {
        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetVolumeBGM(value);
    }

    public void SetVolumeSE(float value)
    {
        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetVolumeSE(value);
    }

    public void SetMuteMaster(bool mute)
    {
        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetMuteMaster(mute);
    }

    public void SetMuteBGM(bool mute)
    {
        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetMuteBGM(mute);
    }

    public void SetMuteSE(bool mute)
    {
        //if (UserSettingsManager.Inst != null) UserSettingsManager.Inst.SetMuteSE(mute);
    }

    /// <summary>一時停止時に呼ぶ（ポーズ画面など）</summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        ApplyVolumeAndMute();
        if (BGMsource == null) return;
        if (paused)
            BGMsource.Pause();
        else
            BGMsource.UnPause();
    }
    #endregion

    #region -- BGM --
    public void PlayBGM(int _index)
    {
        var getData = ResolveBGMData(_index);
        if (getData == null) return;
        if (IsSameBGM(getData.index))
        {
            ChangeVolume_ForBGM();
            return;
        }

        BGMsource.DOKill();
        BGMsource.Stop();
        soundData_BGM = getData;
        currentBGMIndex = getData.index;
        BGMsource.clip = soundData_BGM.clip;
        BGMsource.Play();
        ChangeVolume_ForBGM();
    }

    /// <summary>BGMをフェード付きで再生</summary>
    public void PlayBGMWithFade(int _index, float fadeDuration = -1f)
    {
        var getData = ResolveBGMData(_index);
        if (getData == null) return;
        if (IsSameBGM(getData.index))
        {
            ChangeVolume_ForBGM();
            return;
        }

        var d = fadeDuration > 0 ? fadeDuration : BGM_FADE_DURATION;
        BGMsource.DOKill();
        BGMsource.Stop();
        soundData_BGM = getData;
        currentBGMIndex = getData.index;
        BGMsource.clip = soundData_BGM.clip;
        BGMsource.volume = 0f;
        BGMsource.Play();
        var targetVol = GetBGMVolumeMultiplier() == 0f ? 0f : soundData_BGM.Volume * GetBGMVolumeMultiplier();
        BGMsource.DOFade(targetVol, d).SetUpdate(true);
    }

    public void StopBGM()
    {
        BGMsource.DOKill();
        BGMsource.Stop();
        BGMsource.clip = null;
        soundData_BGM = null;
        currentBGMIndex = -1;
    }

    /// <summary>BGMをフェードアウトして停止</summary>
    public void StopBGMWithFade(float fadeDuration = -1f, Action onComplete = null)
    {
        var d = fadeDuration > 0 ? fadeDuration : BGM_FADE_DURATION;
        BGMsource.DOKill();
        BGMsource.DOFade(0f, d).SetUpdate(true).OnComplete(() =>
        {
            BGMsource.Stop();
            BGMsource.clip = null;
            soundData_BGM = null;
            currentBGMIndex = -1;
            onComplete?.Invoke();
        });
    }

    public void ChangeVolume_ForBGM()
    {
        if (BGMsource == null) return;
        BGMsource.mute = Mute_Master || Mute_BGM || isPaused;
        float bgmBaseVolume = (soundData_BGM != null) ? soundData_BGM.Volume : 1f;
        BGMsource.volume = (Mute_Master || Mute_BGM || isPaused) ? 0f : bgmBaseVolume * GetBGMVolumeMultiplier();
    }

    private bool IsSameBGM(int index)
    {
        return currentBGMIndex == index && BGMsource != null && BGMsource.clip != null;
    }

    private static SO_SoundElement ResolveBGMData(int index)
    {
        if (SOLoader.SoundData == null) return null;
        var data = SOLoader.SoundData.Get_SoundData_BGM(index);
        if (data != null && data.clip != null) return data;

        var options = SOLoader.SoundData.GetSelectableBGMs();
        return (options != null && options.Length > 0) ? options[0] : null;
    }
    #endregion



    #region -- SE --
    /// <summary>インデックス指定でSE再生（汎用）。forcePlay なら空きがなくても既存SEを中断して鳴らす</summary>
    public void PlaySE(int index, bool forcePlay = false)
    {
        var getData = SOLoader.SoundData.Get_SoundData_SE(index);
        if (getData == null) return;
        PlaySE(getData, forcePlay);
    }
    /// <summary>主にボタンクリックなどのUI再生用。forcePlay なら空きがなくても既存SEを中断して鳴らす</summary>
    public void PlaySE_UI(int index, bool forcePlay = false)
    {
        var getData = SOLoader.SoundData.Get_SoundData_SE_UI(index);
        if (getData == null) return;
        PlaySE(getData, forcePlay);
    }

    public void PlaySE(SO_SoundElement getData, bool forcePlay = false)
    {
        if (getData == null || getData.clip == null) return;

        AudioSource target = FindSESource(forcePlay);
        if (target == null) return;

        target.clip = getData.clip;
        target.volume = getData.Volume * GetSEVolumeMultiplier();
        target.pitch = UnityEngine.Random.Range(sePitchMin, sePitchMax);
        target.Play();
    }

    /// <summary>
    /// 空きソースを探す。forcePlay 時は同時再生上限内に空きがなければ、残り時間が最も短い再生中ソースを奪う。
    /// </summary>
    private AudioSource FindSESource(bool forcePlay)
    {
        AudioSource stealCandidate = null;
        float stealRemaining = float.MaxValue;
        int count = 0;

        foreach (AudioSource source in SEsources)
        {
            if (count >= MAX_SIMULTANEOUS_SE) break;
            if (source == null) continue;

            if (!source.isPlaying)
                return source;

            if (forcePlay)
            {
                float remaining = (source.clip != null) ? source.clip.length - source.time : 0f;
                if (remaining < stealRemaining)
                {
                    stealRemaining = remaining;
                    stealCandidate = source;
                }
            }
            count++;
        }

        return forcePlay ? stealCandidate : null;
    }

    public void StopSE()
    {
        foreach (AudioSource source in SEsources)
        {
            source.Stop();
            source.clip = null;
        }
    }
    #endregion
}
