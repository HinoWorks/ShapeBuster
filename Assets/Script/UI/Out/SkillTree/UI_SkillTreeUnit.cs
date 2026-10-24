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
    [SerializeField] int[] neighborSkillIndexes = Array.Empty<int>();
    public bool IsRoot => isRoot;
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


    private UI_SkillTreeManager manager;
    private Action<bool, UI_SkillTreeUnit> onMouseOver;
    private Action<UI_SkillTreeUnit> onClick_Enhance;
    private Action<int, SkillTreeUnlockState, int> onUpdateNodeState;
    private int initVersion;



#if UNITY_EDITOR
    public void OnValidateCall(SkillTreeBase skillTreeBase, SkillTreeUnit unitData)
    {
        skillTree = skillTreeBase;
        skillTreeUnit = unitData;
        if (image_icon != null)
            image_icon.sprite = skillTree.icon;
        if (obj_debug != null)
            obj_debug.SetActive(true);
        if (tmp_debug != null)
            tmp_debug.SetText($"{skillIndex}");

        if (ui_debug_requiredResources == null) return;
        var resourceTypes = (ResourceType[])Enum.GetValues(typeof(ResourceType));
        for (int i = 0; i < ui_debug_requiredResources.Length; i++)
        {
            if (ui_debug_requiredResources[i] == null) continue;
            int count = i < resourceTypes.Length ? skillTreeUnit.CostOf(resourceTypes[i]) : 0;
            ui_debug_requiredResources[i].SetResourceCount(count);
        }
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
            button.onMouseOver += OnPointerEnter;
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
        var baseData = unitData != null ? table.GetSkillTreeBaseData(unitData.baseSkillIndex) : null;
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
            unlockState = isRoot ? SkillTreeUnlockState.EnhanceReady : SkillTreeUnlockState.Hide;
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
            if (!HasPrerequisite())
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

        isEnhanceReady = manager.IsResourceEnough(skillTreeUnit);
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

    public void OnClick_Enhance()
    {
        onClick_Enhance?.Invoke(this);
    }
}
