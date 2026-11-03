using UniRx;
using UnityEngine;
using UnityEngine.UI;


public enum OutGame_MenuType
{
    Loading,
    SkillTree,
    MileStone,
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

    [SerializeField] UI_MileStoneManager ui_mileStoneManager;
    public UI_MileStoneManager UI_MileStoneManager => ui_mileStoneManager;

    [SerializeField] UI_StageSelect ui_stageSelect;



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
        if (ui_mileStoneManager != null)
            ui_mileStoneManager.Init_Once();

        Init(false);
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
            case GameStateType.InGame:
                ShowUpgrade(state == GameStateType.InGame_Ready);
                break;
            default:
                Init(false);
                break;
        }
    }

    void ShowUpgrade(bool resetTab)
    {
        ui_stageSelect?.Close();
        HideModeSwitchButtons();
        HideFullScreenBack();
        ResourceCounterUpdateCheck();
        Init(true);
        if (!resetTab) return;

        currentState = OutGame_MenuType.Loading;
        if (ui_mileStoneManager != null)
            ui_mileStoneManager.RefreshUnits();
        ActiveTabChange(OutGame_MenuType.SkillTree);
    }

    void HideFullScreenBack()
    {
        var back = transform.Find("back") as RectTransform;
        if (back == null) return;
        bool coversScreen = back.anchorMin.x <= 0.01f
            && back.anchorMin.y <= 0.01f
            && back.anchorMax.x >= 0.99f
            && back.anchorMax.y >= 0.99f;
        if (!coversScreen) return;
        back.gameObject.SetActive(false);
    }

    void HideModeSwitchButtons()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null) continue;
            int count = button.onClick.GetPersistentEventCount();
            for (int call = 0; call < count; call++)
            {
                string method = button.onClick.GetPersistentMethodName(call);
                if (method != nameof(OnClick_ToIngame) && method != nameof(OnClick_ToTitle))
                    continue;
                button.gameObject.SetActive(false);
                break;
            }
        }
    }


    void Init(bool show)
    {
        if (!show) ui_stageSelect?.Close();
        if (canvas != null) canvas.enabled = show;
        if (raycaster != null) raycaster.enabled = show;
    }

    private void ResourceCounterUpdateCheck()
    {
        foreach (var ui_resourceCounter in ui_resourceCounters)
        {
            ui_resourceCounter.CounterUpdateCheck();
        }
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
        ui_mileStoneManager.Init(menuType == OutGame_MenuType.MileStone);
    }



    #region -- OnClick --
    public void OnClick_ToIngame()
    {
    }

    public void OnClick_ToTitle()
    {
    }
    #endregion
}
