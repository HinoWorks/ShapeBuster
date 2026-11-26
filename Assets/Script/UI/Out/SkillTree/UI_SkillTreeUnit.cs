using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using Cysharp.Threading.Tasks;


public class UI_SkillTreeUnit : MonoBehaviour
{
    public int skillIndex;
    public SkillTreeBase skillTree;
    public SkillTreeUnit skillTreeUnit;
    public int level { get; private set; } = 0;
    public bool isEnhanceReady { get; private set; } = false;
    public SkillTreeUnlockState unlockState;

    [Space(10)]
    [Header("前提")]
    [SerializeField] bool isRoot;
    [Tooltip("前提のスキルがある方向（isRoot 以外）。その方向に隣り合うスキルを、どちらか取れば開く")]
    [SerializeField] SkillTreeDirection prerequisiteDirectionA;
    [Tooltip("前提の方向の 2 つめ。使わないなら None")]
    [SerializeField] SkillTreeDirection prerequisiteDirectionB;
    [Tooltip("前提のスキルまでのマス数。ふつうは 1（すぐ隣）。間をあけてつなぐときに増やす")]
    [SerializeField, Min(1)] int prerequisiteSteps = 1;
    [Tooltip("前提のスキルのほかに要る開放条件。満たすまで、まだ取っていなければ隠れたまま")]
    [SerializeField] SkillTreeUnlockGate unlockGate;
    [Tooltip("上の方向から UI_SkillTreeManager が入れる（手では書かない）")]
    [SerializeField] int[] neighborSkillIndexes = Array.Empty<int>();
    public bool IsRoot => isRoot;
    public SkillTreeDirection PrerequisiteDirectionA => prerequisiteDirectionA;
    public SkillTreeDirection PrerequisiteDirectionB => prerequisiteDirectionB;
    public int PrerequisiteSteps => Mathf.Max(1, prerequisiteSteps);
    public SkillTreeUnlockGate UnlockGate => unlockGate;
    public int[] NeighborSkillIndexes => neighborSkillIndexes;


    [Space(10)]
    [Header("Connect")]
    [SerializeField] Image image_icon;
    [SerializeField] GameObject obj_lock;
    // [SerializeField] GameObject obj_enhanceReady;
    [SerializeField] GameObject obj_complete;
    [SerializeField] GameObject obj_upgradeable;
    [SerializeField] ParticleSystem eff_enhance;
    [SerializeField] HButton button;

    [Space(10)]
    [Header("DEBUG VIEW")]
    [SerializeField] GameObject obj_debug;
    [SerializeField] TextMeshProUGUI tmp_debug;
    [SerializeField] DEBUG_UI_ResourceUnit[] ui_debug_requiredResources;
    [SerializeField] Image image_debugCost; //Lv1 で払う資源のアイコン
    [SerializeField] TextMeshProUGUI tmp_debugCost; //Lv1 で払う量


    private UI_SkillTreeManager manager;
    private Action<bool, UI_SkillTreeUnit> onMouseOver;
    private Action<UI_SkillTreeUnit> onClick_Enhance;
    private Action<int, SkillTreeUnlockState, int> onUpdateNodeState;
    private int initVersion;



    void Awake()
    {
        if (obj_debug != null)
            obj_debug.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        ShowDebugIndex();
    }

    public void OnValidateCall(SkillTreeBase skillTreeBase, SkillTreeUnit unitData)
    {
        skillTree = skillTreeBase;
        skillTreeUnit = unitData;
        if (image_icon != null)
            image_icon.sprite = skillTree.icon;
        ShowDebugIndex();
        ShowDebugCost();

        if (ui_debug_requiredResources == null) return;
        var resourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        for (int i = 0; i < ui_debug_requiredResources.Length; i++)
        {
            if (ui_debug_requiredResources[i] == null) continue;
            int count = i < resourceTypes.Length ? skillTreeUnit.CostOf(resourceTypes[i]) : 0;
            ui_debug_requiredResources[i].SetResourceCount(count);
        }
    }

    void ShowDebugIndex()
    {
        if (obj_debug != null)
            obj_debug.SetActive(true);
        if (tmp_debug == null) return;

        string name = JapaneseSkillName();
        tmp_debug.SetText(string.IsNullOrEmpty(name) ? skillIndex.ToString() : skillIndex + "\n" + name);
    }

    /// <summary>Lv1 を取るのに要る資源と量を出す。資源はスキルごとに 1 つ（SkillCost シートの resource）。</summary>
    void ShowDebugCost()
    {
        var table = SOLoader.SkillTreeData;
        var cost = table != null ? table.GetSkillTreeUnitCost(skillIndex, 1) : null;
        var costs = cost != null ? cost.CopyPositiveCosts() : Array.Empty<ResourceCount>();
        bool has = costs.Length > 0;

        if (tmp_debugCost != null)
            tmp_debugCost.SetText(has ? costs[0].requiredCount.ToString("N0") : "-");
        if (image_debugCost != null)
        {
            var data = has && SOLoader.ResourceData != null ? SOLoader.ResourceData.GetResourceData(costs[0].resourceType) : null;
            if (data != null && data.icon != null)
                image_debugCost.sprite = data.icon;
            image_debugCost.enabled = has;
        }
    }

    string JapaneseSkillName()
    {
        string fromLocale = JapaneseLocale(Loc.SkillName(skillIndex));
        if (!string.IsNullOrEmpty(fromLocale)) return fromLocale;

        var table = SOLoader.SkillTreeData;
        var basis = table != null ? table.GetSkillTreeBaseData(skillIndex) : skillTree;
        if (basis == null) return "";
        if (!string.IsNullOrEmpty(basis.jpSkillName)) return basis.jpSkillName;
        return basis.skillName ?? "";
    }

    static string JapaneseLocale(string key)
    {
        var table = SOLoader.Locale;
        var entries = table != null ? table.entries : null;
        if (entries == null || string.IsNullOrEmpty(key)) return "";
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry != null && entry.key == key && !string.IsNullOrEmpty(entry.ja))
                return entry.ja;
        }
        return "";
    }
#endif



    public void AwakeCall(UI_SkillTreeManager skillTreeManager,
                            Action<bool, UI_SkillTreeUnit> mouseOver,
                            Action<UI_SkillTreeUnit> clickEnhance,
                            Action<int, SkillTreeUnlockState, int> updateNodeState)
    {
        manager = skillTreeManager;
        onMouseOver = mouseOver;
        onClick_Enhance = clickEnhance;
        onUpdateNodeState = updateNodeState;
        if (button != null)
        {
            button.onMouseOver += OnPointerEnter;
            // 強化できたかどうかで音を変える（UI_SkillTreeManager.OnClick_Enhance）ので、押した音は鳴らさない。
            button.ClickSE = SEType.None;
        }
        RebindClick();
        if (obj_debug != null)
            obj_debug.SetActive(false);
        if (eff_enhance != null)
            eff_enhance.gameObject.SetActive(false);
    }

    public void ReloadFromMaster()
    {
        var table = SOLoader.SkillTreeData;
        if (table == null) return;

        var unitData = table.GetSkillTreeUnitData(skillIndex);
        var baseData = unitData != null ? table.GetSkillTreeBaseData(skillIndex) : null;
        if (unitData == null || baseData == null) return;

        skillTree = baseData;
        skillTreeUnit = unitData;
        if (image_icon != null && skillTree.icon != null)
            image_icon.sprite = skillTree.icon;
    }

    public void SetNeighborSkillIndexes(int[] indexes)
    {
        neighborSkillIndexes = indexes ?? Array.Empty<int>();
    }

    public bool HasPrerequisite()
    {
        return !isRoot && neighborSkillIndexes != null && neighborSkillIndexes.Length > 0;
    }

    public bool Requires(int index)
    {
        if (isRoot || neighborSkillIndexes == null) return false;
        for (int i = 0; i < neighborSkillIndexes.Length; i++)
        {
            if (neighborSkillIndexes[i] == index) return true;
        }
        return false;
    }

    public async UniTask Init()
    {
        int version = ++initVersion;
        ReloadFromMaster();

        if (skillTree == null || skillTreeUnit == null)
        {
            level = 0;
            unlockState = SkillTreeUnlockState.Hide;
            gameObject.SetActive(false);
            if (version != initVersion) return;
            PublishState();
            return;
        }

        int savedLevel = 0;
        if (SaveLoader.Inst != null)
        {
            var skillTreeData = await SaveLoader.Inst.Get_SkillTreeData(skillIndex);
            if (version != initVersion) return;
            savedLevel = skillTreeData != null ? Mathf.Max(0, skillTreeData.level) : 0;
        }
        int maxLevel = Mathf.Max(1, skillTree.maxLevel);
        if (savedLevel > 0)
        {
            level = savedLevel;
            unlockState = level >= maxLevel
                ? SkillTreeUnlockState.EnhanceComplete
                : SkillTreeUnlockState.EnhanceReady;
        }
        else
        {
            level = 0;
            if (!IsGateOpen())
            {
                unlockState = SkillTreeUnlockState.Hide;
            }
            else if (!HasPrerequisite())
            {
                unlockState = SkillTreeUnlockState.EnhanceReady;
            }
            else
            {
                bool acquired = await IsAnyBaseSkillAcquired();
                if (version != initVersion) return;
                unlockState = acquired ? SkillTreeUnlockState.EnhanceReady : SkillTreeUnlockState.Hide;
            }
        }

        SkillTreeBonus.SetLevel(skillIndex, skillTree, level);
        PublishState();
    }

    /// <summary>前提のスキルのほかの開放条件を満たしている。</summary>
    public bool IsGateOpen()
    {
        switch (unlockGate)
        {
            case SkillTreeUnlockGate.LastJournalPage:
                return JournalRuntime.UnlockedPages >= JournalRuntime.PageCount;
            default:
                return true;
        }
    }

    async UniTask<bool> IsAnyBaseSkillAcquired()
    {
        if (neighborSkillIndexes == null || SaveLoader.Inst == null) return false;

        foreach (var index in neighborSkillIndexes)
        {
            if (index < 0) continue;
            var baseSkillData = await SaveLoader.Inst.Get_SkillTreeData(index);
            if (baseSkillData != null && baseSkillData.level > 0)
                return true;
        }
        return false;
    }

    void PublishState()
    {
        onUpdateNodeState?.Invoke(skillIndex, unlockState, level);
        SetState();
    }

    private void SetState()
    {
        bool visible = unlockState != SkillTreeUnlockState.Hide;
        if (visible)
            gameObject.SetActive(true);

        if (image_icon != null)
            image_icon.enabled = visible;
        if (obj_lock != null)
            obj_lock.SetActive(unlockState == SkillTreeUnlockState.Locked);
        //if (obj_enhanceReady != null) obj_enhanceReady.SetActive(unlockState == SkillTreeUnlockState.EnhanceReady);
        if (obj_complete != null)
            obj_complete.SetActive(unlockState == SkillTreeUnlockState.EnhanceComplete);

        if (button != null)
        {
            bool buttonIsSelf = button.gameObject == gameObject;
            if (!buttonIsSelf)
                button.gameObject.SetActive(visible);
            button.enabled = visible;
            if (visible)
            {
                button.Set_Interactable(unlockState == SkillTreeUnlockState.EnhanceReady
                    || unlockState == SkillTreeUnlockState.EnhanceComplete);
            }
        }
        Set_UpgradeVector();
    }

    public bool Set_UpgradeVector()
    {
        if (unlockState != SkillTreeUnlockState.EnhanceReady || skillTreeUnit == null || manager == null)
        {
            isEnhanceReady = false;
            if (obj_upgradeable != null) obj_upgradeable.SetActive(false);
            return false;
        }

        isEnhanceReady = manager.IsResourceEnough(this);
        if (obj_upgradeable != null) obj_upgradeable.SetActive(isEnhanceReady);
        return isEnhanceReady;
    }


    public async UniTask CallBack_Enhance()
    {
        PlayEnhanceEffect().Forget();
        await Init();
    }

    private async UniTaskVoid PlayEnhanceEffect()
    {
        if (eff_enhance == null) return;

        var effectObj = eff_enhance.gameObject;
        effectObj.SetActive(true);
        eff_enhance.Play(true);

        var canceled = await UniTask.WaitUntil(
            () => effectObj == null || !effectObj.activeInHierarchy || !eff_enhance.IsAlive(true),
            cancellationToken: this.GetCancellationTokenOnDestroy()
        ).SuppressCancellationThrow();

        if (canceled || effectObj == null) return;
        effectObj.SetActive(false);
    }

    private void OnPointerEnter(bool isEnter)
    {
        if (unlockState == SkillTreeUnlockState.Hide) return;
        onMouseOver?.Invoke(isEnter, this);
    }

    public void RebindClick()
    {
        if (button == null) return;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OnClick_Enhance);
    }

    public void OnClick_Enhance()
    {
        onClick_Enhance?.Invoke(this);
    }
}
