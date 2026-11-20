using System;
using UnityEngine;

/// <summary>数字の表し方。</summary>
public enum NumberNotation
{
    /// <summary>1.2K、3.4M のように、単位の記号で縮める。</summary>
    Short = 0,
    /// <summary>1.23e5 のように、指数で書く。</summary>
    Scientific = 1,
}

/// <summary>
/// オプションメニューの設定のうち、音（SoundManager）と言語（Loc）以外のもの。
/// どれも PlayerPrefs に保存するので、セーブデータを消しても残る。
/// 値を変えると Changed を出す。画面モード・フレームレート・裏で動かすかは、変えたときにすぐ反映する。
/// </summary>
public static class GameOptions
{
    const string KeyShake = "option_shake";
    const string KeyDamageNumber = "option_damageNumber";
    const string KeyNotation = "option_notation";
    const string KeyFrameRate = "option_frameRate";
    const string KeyFullscreen = "option_fullscreen";
    const string KeyBackground = "option_background";

    /// <summary>フレームレートの選択肢。0 はモニターに合わせる（垂直同期）。</summary>
    public static readonly int[] FrameRates = { 0, 30, 60, 120 };

    /// <summary>ウィンドウにしたときの大きさ。</summary>
    const int WindowWidth = 1280;
    const int WindowHeight = 720;

    static bool loaded;
    static bool screenShake = true;
    static bool damageNumbers = true;
    static NumberNotation notation = NumberNotation.Short;
    static int frameRateIndex;
    static bool fullscreen = true;
    static bool runInBackground = true;

    public static event Action Changed;

    /// <summary>画面を揺らす。</summary>
    public static bool ScreenShake { get { Ensure(); return screenShake; } }
    /// <summary>ダメージの数字を出す。</summary>
    public static bool DamageNumbers { get { Ensure(); return damageNumbers; } }
    public static NumberNotation Notation { get { Ensure(); return notation; } }
    /// <summary>FrameRates の何番目か。</summary>
    public static int FrameRateIndex { get { Ensure(); return frameRateIndex; } }
    public static bool Fullscreen { get { Ensure(); return fullscreen; } }
    /// <summary>ウィンドウが選ばれていないときも動かし続ける（変換が止まらない）。</summary>
    public static bool RunInBackground { get { Ensure(); return runInBackground; } }

    public static void SetScreenShake(bool value) { Ensure(); screenShake = value; Save(); }
    public static void SetDamageNumbers(bool value) { Ensure(); damageNumbers = value; Save(); }
    public static void SetNotation(NumberNotation value) { Ensure(); notation = value; Save(); }
    public static void SetFrameRateIndex(int value) { Ensure(); frameRateIndex = Mathf.Clamp(value, 0, FrameRates.Length - 1); Save(); ApplyFrameRate(); }
    public static void SetFullscreen(bool value) { Ensure(); fullscreen = value; Save(); ApplyScreen(); }
    public static void SetRunInBackground(bool value) { Ensure(); runInBackground = value; Save(); ApplyBackground(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        loaded = false;
        Ensure();
        ApplyFrameRate();
        ApplyBackground();
        // 画面モードは起動時の Unity の設定に任せる（前回の状態は Unity が覚えている）。表示だけ今の状態に合わせる。
#if !UNITY_EDITOR
        fullscreen = Screen.fullScreen;
#endif
    }

    static void Ensure()
    {
        if (loaded) return;
        loaded = true;
        screenShake = PlayerPrefs.GetInt(KeyShake, 1) != 0;
        damageNumbers = PlayerPrefs.GetInt(KeyDamageNumber, 1) != 0;
        notation = (NumberNotation)Mathf.Clamp(PlayerPrefs.GetInt(KeyNotation, 0), 0, 1);
        frameRateIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyFrameRate, 0), 0, FrameRates.Length - 1);
        fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) != 0;
        runInBackground = PlayerPrefs.GetInt(KeyBackground, 1) != 0;
    }

    static void Save()
    {
        PlayerPrefs.SetInt(KeyShake, screenShake ? 1 : 0);
        PlayerPrefs.SetInt(KeyDamageNumber, damageNumbers ? 1 : 0);
        PlayerPrefs.SetInt(KeyNotation, (int)notation);
        PlayerPrefs.SetInt(KeyFrameRate, frameRateIndex);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyBackground, runInBackground ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static void ApplyFrameRate()
    {
        int rate = FrameRates[frameRateIndex];
        if (rate <= 0)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = rate;
        }
    }

    static void ApplyBackground()
    {
        Application.runInBackground = runInBackground;
    }

    static void ApplyScreen()
    {
#if !UNITY_EDITOR
        if (fullscreen)
        {
            var display = Display.main;
            Screen.SetResolution(display.systemWidth, display.systemHeight, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
        }
#endif
    }
}
