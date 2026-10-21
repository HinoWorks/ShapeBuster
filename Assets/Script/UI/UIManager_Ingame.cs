using UnityEngine;
using UnityEngine.Events;
using UniRx;

public class UIManager_Ingame : MonoBehaviour
{
    public static UIManager_Ingame Inst { get; private set; }
    [SerializeField] UI_PlayerHpCont playerHpCont;
    [SerializeField] UI_stageProgressCont stageProgressCont;
    [SerializeField] UI_ResourceCounter[] resourceCounters;

    [Space(10)]
    public UI_ResultManager ui_ResultManager;



    private void Awake()
    {
        if (Inst == null) Inst = this;
        else Destroy(gameObject);

        GameEvent.GameState.SetGameState.Subscribe(OnGameStateChanged).AddTo(this);
    }


    private void Start()
    {
        playerHpCont.InitOnce();
        stageProgressCont.InitOnce();
        foreach (var resourceCounter in resourceCounters)
        {
            resourceCounter.AwakeCall_Ingame();
        }
    }


    private void OnGameStateChanged(GameStateType state)
    {
        if (state == GameStateType.InGame_Ready)
        {
            // リソース初期化
            foreach (var resourceCounter in resourceCounters)
            {
                resourceCounter.Set_Init();
            }
        }
    }
}
