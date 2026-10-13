using UnityEngine;

public class GameWatcher : MonoBehaviour
{
    public static GameWatcher Inst { get; private set; }

    private void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); }
    }

    public GameStateType currentGameState { get; private set; } = GameStateType.Title;
    public bool isInGameNow => currentGameState == GameStateType.InGame;





    void Start()
    {
        currentGameState = GameStateType.Title;
        GameEvent.GameState.PublishGameState(currentGameState);
        GameParamManager.Init();

        IngameReady();
    }



    private void IngameReady()
    {
        EnemyGenerateManager.Inst?.Clear();
        PlayerManager.Inst?.PlayerHPCont?.Init();
        GameParamManager.Init_IngameStart();
        AttackManager.Inst.Set_Ready();
        GameEvent.GameState.PublishGameState(GameStateType.InGame_Ready);

        IngameStart();
    }


    private void IngameStart()
    {
        GameEvent.GameState.PublishGameState(GameStateType.InGame);

        AttackManager.Inst.Set_AttackState(true);
        EnemyGenerateManager.Inst?.Set_GenerateState(true);
    }


    public void SetGameState(GameStateType state)
    {
        if (!GameParamManager.isInitEnd) return;
        currentGameState = state;
        Debug.Log($"<color=yellow> ========= GameState: {currentGameState} ========= </color>");
        GameEvent.GameState.PublishGameState(currentGameState);
    }


}
