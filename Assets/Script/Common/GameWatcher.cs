using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// シーンは切り替えず、状態だけを進める。
/// Title の開始から InGame_Ready、その後は InGame のまま戦闘と強化を続ける。
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
        if (IsRetired(state)) return;
        if (isStateSet && currentGameState == state) return;

        currentGameState = state;
        isStateSet = true;
        Debug.Log($"<color=yellow> ========= GameState: {currentGameState} ========= </color>");
        GameEvent.GameState.PublishGameState(currentGameState);
        OnStateEntered(currentGameState);
    }

    void OnStateEntered(GameStateType state)
    {
        if (state == GameStateType.InGame_Ready)
            BeginIngameReady();
    }

    static bool IsRetired(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_End:
            case GameStateType.Result:
            case GameStateType.ResultEnd_ToOutGame:
            case GameStateType.ResultEnd_ToIngameReady:
            case GameStateType.OutGame:
                return true;
            default:
                return false;
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
        return state == GameStateType.InGame_Ready || state == GameStateType.InGame;
    }
}
