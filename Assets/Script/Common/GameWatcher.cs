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

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); }
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
        PlayerManager.Inst?.Init();
        GameParamManager.Init_IngameStart();
        EnterIngame(version);
    }

    async void EnterIngame(int version)
    {
        await UniTask.Delay(1000, cancellationToken: this.GetCancellationTokenOnDestroy());
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
