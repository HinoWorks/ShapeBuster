using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class UIManager_Ingame : MonoBehaviour
{
    public static UIManager_Ingame Inst { get; private set; }
    [SerializeField] UI_PlayerHpCont playerHpCont;
    [SerializeField] UI_stageProgressCont stageProgressCont;
    [SerializeField] UI_ResourceCounter[] resourceCounters;
    [SerializeField] UI_HUD_MagazineInfo bulletInfo;
    public UI_HUD_MagazineInfo BulletInfo => bulletInfo;

    [Space(10)]
    public UI_ResultManager ui_ResultManager;

    Canvas canvas;
    GraphicRaycaster raycaster;



    private void Awake()
    {
        if (Inst == null) Inst = this;
        else Destroy(gameObject);

        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();

        GameEvent.GameState.SetGameState.Subscribe(OnGameStateChanged).AddTo(this);
        Apply(GameStateType.Title);
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
        Apply(state);
    }

    void Apply(GameStateType state)
    {
        bool show = GameWatcher.IsIngameScreen(state);
        if (canvas != null) canvas.enabled = show;
        if (raycaster != null) raycaster.enabled = show;

        if (state == GameStateType.InGame_Ready)
        {
            foreach (var resourceCounter in resourceCounters)
            {
                resourceCounter.Set_Init();
            }
        }

        if (ui_ResultManager == null) return;
        if (state == GameStateType.Result)
        {
            var record = IngameRecordManager.Inst != null ? IngameRecordManager.Inst.Record : null;
            ui_ResultManager.Show(record);
        }
        else
        {
            ui_ResultManager.Hide();
        }
    }






    #region -- OnClick --
    public void OnClick_ForceEnd()
    {
        PlayerManager.Inst.PlayerHPCont.Set_ForceEnd();
    }
    #endregion
}
