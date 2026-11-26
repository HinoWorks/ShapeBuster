using System;
using UniRx;
using UnityEngine;

/// <summary>総プレイ時間のセーブ用。単位は秒。</summary>
[Serializable]
public class PlayTimeSave
{
    /// <summary>ゲームを開いて、前に出していた時間の合計（タイトル・アウトゲーム・インゲームすべて）。</summary>
    public double totalSeconds;
    /// <summary>そのうちインゲーム（InGame_Ready〜Result）の時間の合計。負けた回も含む。</summary>
    public double ingameSeconds;
}

/// <summary>
/// 総プレイ時間を数える。実時間（Time.unscaledDeltaTime）で数えるので、ゲームの減速や速度の変更に左右されない。
/// ウィンドウが前に出ていない（フォーカスがない）あいだは数えない。1 フレームが長すぎるとき（止まっていた・読み込み中）は 1 秒までにする。
/// セーブは SaveLoader の key_playTime。<see cref="SaveInterval"/> 秒ごとと、バックグラウンドへ入るとき・終了時に書く。
/// レベルデザインの目標（全体 3〜4 時間）と見比べるための記録で、ゲームの中には出さない。確かめ方は
/// SRDebugger の「記録」、またはエディタの Tools → 総プレイ時間を表示。
/// </summary>
public static class PlayTimeRecord
{
    const float SaveInterval = 30f;
    const float MaxStep = 1f;

    static double totalSeconds;
    static double ingameSeconds;
    static bool dirty;
    static float sinceSave;
    static bool inIngame;

    public static double TotalSeconds => totalSeconds;
    public static double IngameSeconds => ingameSeconds;
    public static bool IsDirty => dirty;

    /// <summary>セーブから読んだ値に置き換える。null なら 0 から。</summary>
    public static void Replace(PlayTimeSave save)
    {
        totalSeconds = save != null ? Math.Max(0d, save.totalSeconds) : 0d;
        ingameSeconds = save != null ? Math.Max(0d, save.ingameSeconds) : 0d;
        dirty = false;
        sinceSave = 0f;
    }

    public static PlayTimeSave Capture()
    {
        return new PlayTimeSave { totalSeconds = totalSeconds, ingameSeconds = ingameSeconds };
    }

    public static void MarkSaved()
    {
        dirty = false;
        sinceSave = 0f;
    }

    /// <summary>今たまっている分を書き出す。</summary>
    public static void Flush()
    {
        if (dirty && SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SavePlayTime();
    }

    /// <summary>"12 時間 34 分 56 秒" の形にする。</summary>
    public static string Format(double seconds)
    {
        long s = (long)Math.Max(0d, seconds);
        return $"{s / 3600} 時間 {s / 60 % 60} 分 {s % 60} 秒";
    }

    public static string Summary()
    {
        return $"全体 {Format(totalSeconds)}（インゲーム {Format(ingameSeconds)}）";
    }

    static void Tick()
    {
        if (SaveLoader.Inst == null || SaveLoader.Inst.currentState == state.InitialLoad) return;
        if (!Application.isFocused) return;

        float step = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        if (step <= 0f) return;
        totalSeconds += step;
        if (inIngame)
            ingameSeconds += step;
        dirty = true;

        sinceSave += step;
        if (sinceSave >= SaveInterval)
            Flush();
    }

    static void OnGameState(GameStateType state)
    {
        inIngame = GameWatcher.IsIngameScreen(state);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        totalSeconds = 0d;
        ingameSeconds = 0d;
        dirty = false;
        sinceSave = 0f;
        inIngame = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateRunner()
    {
        var root = new GameObject("PlayTimeRecord");
        UnityEngine.Object.DontDestroyOnLoad(root);
        root.hideFlags = HideFlags.HideInHierarchy;
        root.AddComponent<Runner>();
    }

    /// <summary>毎フレーム数えるための、見えないオブジェクト。</summary>
    sealed class Runner : MonoBehaviour
    {
        void Awake()
        {
            GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
        }

        void Update()
        {
            Tick();
        }
    }
}
