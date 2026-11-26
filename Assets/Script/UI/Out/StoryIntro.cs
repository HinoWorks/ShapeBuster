using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 初めて Start を押したときに出す、物語の始まりの文。
/// タイトルの Start（初めてのプレイ）では、文のあとアウトゲームを通らずに最初の段を始める（<see cref="StartFirstStage"/>）。
/// アウトゲームの Start からも呼ぶ（まだ出していないとき）。
/// 暗転 → 真っ黒な画面に文を 1 行ずつ出す（story.intro1〜）→ 暗いうちに次へ進める。
/// 次がそのままインゲームへの切り替え（初めてのプレイは段 0 だけなので、ステージ選択を出さない）のときは、
/// 画面の幕が閉じきったところで黒を消し、幕が開くとインゲームが見える。そうでなければ黒を明るく戻す。
/// 出したことは SaveLoader の「出したチュートリアル」の番号 <see cref="ShownIndex"/> に残す。
/// すでに段をクリアしたことのあるセーブでは出さない。文の出し方はワクチンのそのあとの日記と同じ（VaccineEnding.PlayLines）。
/// </summary>
public static class StoryIntro
{
    /// <summary>SaveLoader の IsTutorialShown／Request_SaveTutorialShown で使う番号。</summary>
    public const int ShownIndex = 1000;
    // 画面の幕（TransitionCurtain は 32000）より下、ほかの UI より上。
    const int SortingOrder = 31000;
    const int Lines = 3;
    const float FadeOutDuration = 0.8f;
    const float BlackHold = 0.4f;
    const float FadeInDuration = 0.8f;

    public static bool Playing { get; private set; }

    /// <summary>まだ出していない、初めてのプレイ。</summary>
    public static bool ShouldPlay
    {
        get
        {
            var save = SaveLoader.Inst;
            if (save == null) return false;
            return save.ClearedStageIndex < 0 && !save.IsTutorialShown(ShownIndex);
        }
    }

    /// <summary>演出を始める。暗転したところで next を呼ぶ。始められたら true。</summary>
    public static bool Play(Action next)
    {
        if (Playing) return false;
        var watcher = GameWatcher.Inst;
        if (watcher == null || watcher.IsChangingScreen) return false;
        PlayAsync(next).Forget();
        return true;
    }

    /// <summary>最初の段を選んで、インゲームへ切り替える。初めてのプレイで、ステージ選択もスキルツリーも挟まないときに使う。</summary>
    public static void StartFirstStage()
    {
        var stageTime = StageTimeManager.Inst;
        if (stageTime != null)
            stageTime.SetStageIndex(FirstStageIndex());
        else
            Debug.LogWarning("StageTimeManager がないため、最初の段を選べません。");
        GameWatcher.Inst.ChangeScreen(GameStateType.InGame_Ready);
    }

    static int FirstStageIndex()
    {
        var data = SOLoader.StageData;
        if (data == null || data.stageDatas == null) return 0;
        int first = int.MaxValue;
        foreach (var stage in data.stageDatas)
        {
            if (stage != null && stage.stageIndex < first)
                first = stage.stageIndex;
        }
        return first == int.MaxValue ? 0 : first;
    }

    static async UniTaskVoid PlayAsync(Action next)
    {
        Playing = true;
        // 途中で閉じても、次の起動でまた出ないように先に記録する。
        if (SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SaveTutorialShown(ShownIndex);

        var root = BuildCanvas(out var black);
        var token = root.GetCancellationTokenOnDestroy();
        bool nextCalled = false;
        try
        {
            if (SoundManager.Inst != null)
                SoundManager.Inst.SetCurtainMuffle(true, FadeOutDuration);
            black.DOFade(1f, FadeOutDuration).SetEase(Ease.InQuad).SetUpdate(true);
            await VaccineEnding.Wait(FadeOutDuration, token);
            black.color = Color.black;
            await VaccineEnding.Wait(BlackHold, token);

            await VaccineEnding.PlayLines(root.transform, "story.intro", Lines, token);

            nextCalled = true;
            next?.Invoke();
            await UniTask.DelayFrame(2, cancellationToken: token);

            var watcher = GameWatcher.Inst;
            if (watcher != null && watcher.IsChangingScreen)
            {
                // 幕（黒より上）が閉じきったら黒を消す。音のこもりは幕が戻す。
                var curtain = TransitionCurtain.Inst;
                await UniTask.WaitUntil(() => curtain == null || !watcher.IsChangingScreen
                    || (curtain.IsCovering && !curtain.IsMoving), cancellationToken: token);
                black.color = new Color(0f, 0f, 0f, 0f);
                await UniTask.WaitUntil(() => !watcher.IsChangingScreen, cancellationToken: token);
                return;
            }

            if (SoundManager.Inst != null)
                SoundManager.Inst.SetCurtainMuffle(false, FadeInDuration);
            black.DOFade(0f, FadeInDuration).SetEase(Ease.OutQuad).SetUpdate(true);
            await VaccineEnding.Wait(FadeInDuration, token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Playing = false;
            if (root != null)
            {
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                    graphic.DOKill();
                UnityEngine.Object.Destroy(root);
            }
            if (!nextCalled && SoundManager.Inst != null)
                SoundManager.Inst.SetCurtainMuffle(false, 0.2f);
        }
    }

    static GameObject BuildCanvas(out Image black)
    {
        var root = new GameObject("StoryIntro");
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        // 演出の間は下の画面を押せないようにする（透明な板で受ける）。
        var blocker = VaccineEnding.FullScreen("blocker", root.transform);
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        black = VaccineEnding.FullScreen("black", root.transform);
        black.color = new Color(0f, 0f, 0f, 0f);
        return root;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Playing = false;
    }
}
