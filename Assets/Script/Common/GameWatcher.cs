using System;
using UniRx;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// シーンは切り替えず、状態だけを進める。
/// Title → OutGame → InGame_Ready → InGame → InGame_End → Result
/// Result からは OutGame か、もう一度 InGame_Ready。
/// </summary>
public class GameWatcher : MonoBehaviour
{
    public static GameWatcher Inst { get; private set; }

    public GameStateType currentGameState { get; private set; } = GameStateType.Title;
    public bool isInGameNow => currentGameState == GameStateType.InGame;

    bool isStateSet;
    int ingameReadyVersion;
    bool screenChanging;

    // InGame_Ready から InGame へ進むまでの待ち（ミリ秒）。幕で隠れているので短くてよい。
    const int IngameReadyDelayMs = 250;
    // 幕を閉じている最短の時間（秒）。この間、真ん中のゾンビが 1 回跳ねる（シェーダーの _HopSpeed = 8 で 1 回 0.39 秒）。
    const float MinCurtainHold = 0.39f;
    // 準備ができた合図が来ないときに、あきらめて幕を開けるまでの時間（秒）。
    const float CurtainTimeout = 5f;

    /// <summary>幕を使った画面切り替えの途中。</summary>
    public bool IsChangingScreen => screenChanging;

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        BlockSession.Ensure();
    }

    void Start()
    {
        GameParamManager.Init();
        SetGameState(GameStateType.Title);
    }

    public void SetGameState(GameStateType state)
    {
        if (state != GameStateType.Title && !GameParamManager.isInitEnd)
        {
            SetGameStateWhenReady(state);
            return;
        }

        ApplyState(state);
    }

    /// <summary>
    /// 幕を閉じてから状態を進め、準備ができたら幕を開ける。インゲームとアウトゲームを行き来するときに使う。
    /// インゲームへ入るときは、BlockSession が場を並べ終えた合図（GameEvent.InGame.Started）を待つ。
    /// 切り替えの途中に呼ばれたら無視する。
    /// </summary>
    public void ChangeScreen(GameStateType state)
    {
        if (screenChanging) return;
        ChangeScreenAsync(state).Forget();
    }

    async UniTaskVoid ChangeScreenAsync(GameStateType state)
    {
        screenChanging = true;
        var token = this.GetCancellationTokenOnDestroy();
        var curtain = TransitionCurtain.Ensure();
        IDisposable startedSub = null;
        try
        {
            await curtain.Close(token);
            float closedAt = Time.unscaledTime;

            bool toIngame = state == GameStateType.InGame_Ready || state == GameStateType.ResultEnd_ToIngameReady;
            bool ready = !toIngame;
            if (toIngame)
                startedSub = GameEvent.InGame.Started.Subscribe(_ => ready = true);

            SetGameState(state);
            // 画面の切り替えは状態の通知で一斉に起きるので、アウトゲームは数フレーム待てば並び終わる。
            await UniTask.DelayFrame(2, cancellationToken: token);
            await UniTask.WaitUntil(() => ready || Time.unscaledTime - closedAt > CurtainTimeout, cancellationToken: token);
            if (!ready)
                Debug.LogWarning("GameWatcher：インゲームの準備ができた合図が来ないまま、幕を開けます。");

            float hold = MinCurtainHold - (Time.unscaledTime - closedAt);
            if (hold > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(hold), DelayType.UnscaledDeltaTime, cancellationToken: token);

            await curtain.Open(token);
        }
        finally
        {
            startedSub?.Dispose();
            screenChanging = false;
        }
    }

    async void SetGameStateWhenReady(GameStateType state)
    {
        await UniTask.WaitUntil(() => GameParamManager.isInitEnd, cancellationToken: this.GetCancellationTokenOnDestroy());
        ApplyState(state);
    }

    void ApplyState(GameStateType state)
    {
        if (isStateSet && currentGameState == state) return;

        // プレイ中だけ終了へ進める。リザルト表示後の連続ダメージで巻き戻さない
        if (state == GameStateType.InGame_End && currentGameState != GameStateType.InGame)
            return;

        currentGameState = state;
        isStateSet = true;
        Debug.Log($"<color=yellow> ========= GameState: {currentGameState} ========= </color>");
        GameEvent.GameState.PublishGameState(currentGameState);
        OnStateEntered(currentGameState);
    }

    void OnStateEntered(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
                BeginIngameReady();
                break;
            case GameStateType.InGame_End:
                SetGameState(GameStateType.Result);
                break;
            case GameStateType.ResultEnd_ToOutGame:
                SetGameState(GameStateType.OutGame);
                break;
            case GameStateType.ResultEnd_ToIngameReady:
                SetGameState(GameStateType.InGame_Ready);
                break;
        }
    }

    void BeginIngameReady()
    {
        int version = ++ingameReadyVersion;
        GameParamManager.Init_IngameStart();
        EnterIngame(version);
    }

    async void EnterIngame(int version)
    {
        await UniTask.Delay(IngameReadyDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: this.GetCancellationTokenOnDestroy());
        if (version != ingameReadyVersion) return;
        if (currentGameState != GameStateType.InGame_Ready) return;
        SetGameState(GameStateType.InGame);
    }


    public static bool IsIngameScreen(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
            case GameStateType.InGame:
            case GameStateType.InGame_End:
            case GameStateType.Result:
                return true;
            default:
                return false;
        }
    }
}
