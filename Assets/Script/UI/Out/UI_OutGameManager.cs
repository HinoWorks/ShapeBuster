using UniRx;
using UnityEngine;
using UnityEngine.UI;


public enum OutGame_MenuType
{
    Loading,
    SkillTree,
}

public class UI_OutGameManager : MonoBehaviour
{
    public static UI_OutGameManager Inst { get; private set; }

    Canvas canvas;
    GraphicRaycaster raycaster;

    private OutGame_MenuType currentState = OutGame_MenuType.Loading;

    [SerializeField] UI_OutGameTabBase[] ui_outGameTabs;
    [SerializeField] UI_ResourceCounter[] ui_resourceCounters;


    [Space(10)]
    [SerializeField] UI_SkillTreeManager ui_skillTreeManager;
    public UI_SkillTreeManager UI_SkillTreeManager => ui_skillTreeManager;



    void Awake()
    {
        Inst = this;
        canvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);

        foreach (var ui_outGameTab in ui_outGameTabs)
        {
            ui_outGameTab.Start_OnceInit();
        }
        foreach (var ui_resourceCounter in ui_resourceCounters)
        {
            ui_resourceCounter.AwakeCall_OutGame();
        }

        ui_skillTreeManager.Init_Once();

        Init(false);
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.OutGame:
                ToOutGame();
                break;
            default:
                Init(false);
                break;
        }
    }

    private void ToOutGame()
    {
        currentState = OutGame_MenuType.Loading;
        Init(true);
        ActiveTabChange(OutGame_MenuType.SkillTree);
    }


    void Init(bool show)
    {
        if (canvas != null) canvas.enabled = show;
        if (raycaster != null) raycaster.enabled = show;
    }


    public void ActiveTabChange(OutGame_MenuType menuType)
    {
        if (currentState == menuType) return;
        currentState = menuType;
        foreach (var ui_outGameTab in ui_outGameTabs)
        {
            ui_outGameTab.Init(menuType);
        }

        ui_skillTreeManager.Init(menuType == OutGame_MenuType.SkillTree);
    }



    #region -- OnClick --
    public void OnClick_ToIngame()
    {
        GameWatcher.Inst.SetGameState(GameStateType.InGame_Ready);
    }

    public void OnClick_ToTitle()
    {
        GameWatcher.Inst.SetGameState(GameStateType.Title);
    }
    #endregion
}
