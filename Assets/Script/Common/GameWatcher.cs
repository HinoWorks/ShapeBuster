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

        IngameStart();
    }



    private void IngameStart()
    {
        GameParamManager.Init_IngameStart();
        AttackManager.Inst.Set_Ready();
    }


    public void SetGameState(GameStateType state)
    {
        if (!GameParamManager.isInitEnd) return;
        currentGameState = state;
        Debug.Log($"<color=yellow> ========= GameState: {currentGameState} ========= </color>");
        GameEvent.GameState.PublishGameState(currentGameState);
    }


}
