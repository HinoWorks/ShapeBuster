using UniRx;
using UnityEngine;
using UnityEngine.UI;


public enum OutGame_MenuType
{
    Loading,
    SkillTree,
    MileStone,
    Convert,
    Bestiary,
    Vaccine,
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

    [SerializeField] UI_BestiaryManager ui_bestiaryManager;
    [Tooltip("図鑑タブの「NEW」の印。発見して、まだ開いていない項目があるときに出す")]
    [SerializeField] GameObject obj_bestiaryNew;

    [SerializeField] UI_VaccineManager ui_vaccineManager;

    [SerializeField] UI_StageSelect ui_stageSelect;
    [SerializeField] UI_ConvertPanel convertPanel;



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
        if (ui_bestiaryManager != null)
            ui_bestiaryManager.Init_Once();
        BestiaryRuntime.Changed.Subscribe(_ => RefreshBestiaryNew()).AddTo(this);
        if (ui_vaccineManager != null)
            ui_vaccineManager.Init_Once();
        convertPanel?.Setup();
        GameEvent.AchieveEvent.SkillTreeUnlock.Subscribe(_ =>
        {
            RefreshUnlockTabs();
        }).AddTo(this);
        RefreshUnlockTabs();

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
        ui_stageSelect?.Close();
        currentState = OutGame_MenuType.Loading;
        ResourceCounterUpdateCheck();
        Init(true);
        if (ui_mileStoneManager != null)
            ui_mileStoneManager.RefreshUnits();

        RefreshUnlockTabs();
        RefreshBestiaryNew();
        ActiveTabChange(OutGame_MenuType.SkillTree);
    }

    void RefreshBestiaryNew()
    {
        if (obj_bestiaryNew != null)
            obj_bestiaryNew.SetActive(BestiaryRuntime.HasUnseen);
    }


    void Init(bool show)
    {
        if (!show)
        {
            ui_stageSelect?.Close();
            convertPanel?.Show(false);
        }
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

    UI_OutGameTabBase FindTab(OutGame_MenuType menuType)
    {
        if (ui_outGameTabs == null) return null;
        for (int i = 0; i < ui_outGameTabs.Length; i++)
        {
            if (ui_outGameTabs[i] != null && ui_outGameTabs[i].MenuType == menuType)
                return ui_outGameTabs[i];
        }
        return null;
    }

    void RefreshUnlockTabs()
    {
        convertPanel?.RefreshUnlock();
        RefreshHeaderTab(OutGame_MenuType.MileStone, SkillTreeBonus.MilestoneUnlocked);
        RefreshHeaderTab(OutGame_MenuType.Convert, SkillTreeBonus.ConvertUnlocked);
        RefreshHeaderTab(OutGame_MenuType.Vaccine, VaccineRuntime.Unlocked);
    }

    void RefreshHeaderTab(OutGame_MenuType menuType, bool open)
    {
        var tab = FindTab(menuType);
        if (tab != null)
            tab.gameObject.SetActive(open);
        if (!open && currentState == menuType)
            ActiveTabChange(OutGame_MenuType.SkillTree);
    }

    public void ActiveTabChange(OutGame_MenuType menuType)
    {
        if (menuType == OutGame_MenuType.MileStone && !SkillTreeBonus.MilestoneUnlocked)
            menuType = OutGame_MenuType.SkillTree;
        if (menuType == OutGame_MenuType.Convert && !SkillTreeBonus.ConvertUnlocked)
            menuType = OutGame_MenuType.SkillTree;
        if (menuType == OutGame_MenuType.Vaccine && !VaccineRuntime.Unlocked)
            menuType = OutGame_MenuType.SkillTree;
        if (currentState == menuType) return;
        currentState = menuType;
        foreach (var ui_outGameTab in ui_outGameTabs)
        {
            ui_outGameTab.Init(menuType);
        }

        ui_skillTreeManager.Init(menuType == OutGame_MenuType.SkillTree);
        ui_mileStoneManager.Init(menuType == OutGame_MenuType.MileStone);
        if (ui_bestiaryManager != null)
            ui_bestiaryManager.Init(menuType == OutGame_MenuType.Bestiary);
        if (ui_vaccineManager != null)
            ui_vaccineManager.Init(menuType == OutGame_MenuType.Vaccine);
        if (convertPanel != null)
            convertPanel.Show(menuType == OutGame_MenuType.Convert);
    }



    #region -- OnClick --
    public void OnClick_ToIngame()
    {
        if (ui_stageSelect != null)
            ui_stageSelect.Open();
        else
            GameWatcher.Inst.ChangeScreen(GameStateType.InGame_Ready);
    }

    public void OnClick_ToTitle()
    {
        GameWatcher.Inst.SetGameState(GameStateType.Title);
    }
    #endregion
}
