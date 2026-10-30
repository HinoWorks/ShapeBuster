using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_SkillTreeDetail : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_skillName;
    [SerializeField] TextMeshProUGUI tmp_description;
    [SerializeField] TextMeshProUGUI tmp_level;
    [SerializeField] TextMeshProUGUI tmp_paramNow;
    [SerializeField] TextMeshProUGUI tmp_paramNext;
    [SerializeField] GameObject obj_param;
    [SerializeField] GameObject obj_paramArrow;
    [SerializeField] GameObject obj_complete;
    [SerializeField] GameObject obj_resourceRoot;
    [SerializeField] UI_ResourceCounter_Simple[] ui_resourceCounts;
    [SerializeField] float hoverYOffset = 200f;


    private UI_SkillTreeUnit currentUnit;
    private List<ResourceCount> requredResources = new List<ResourceCount>();
    public List<ResourceCount> RequredResources => requredResources;
    public bool IsEnhanceReady => resourceReady && !isMaxLevel;
    private bool isMaxLevel = false;
    private bool resourceReady = false;

    private RectTransform rectTr;
    private Canvas rootCanvas;
    private Vector3 anchorWorldPosition;


    public bool IsShowing(UI_SkillTreeUnit skillTreeUnit)
    {
        return currentUnit == skillTreeUnit && gameObject.activeSelf;
    }

    public void Hide()
    {
        SetData(null);
    }

    public void SetData(UI_SkillTreeUnit skillTreeUnit = null)
    {
        if (skillTreeUnit == null)
        {
            gameObject.SetActive(false);
            currentUnit = null;
            requredResources.Clear();
            resourceReady = false;
            isMaxLevel = true;
            return;
        }
        currentUnit = skillTreeUnit;
        SetData_Base(currentUnit.level);
    }

    public void SetPositionWithAutoFlip(Vector3 worldPosition)
    {
        EnsureCachedRefs();
        anchorWorldPosition = worldPosition;
        ApplyVerticalOffset(hoverYOffset);

        Canvas.ForceUpdateCanvases();
        if (IsAnyCornerOutOfScreen())
        {
            ApplyVerticalOffset(-hoverYOffset);
        }
    }

    private void SetData_Base(int currentLevel)
    {
        if (currentUnit == null || currentUnit.skillTree == null) return;

        var so = currentUnit.skillTree;
        int maxLevel = Mathf.Max(1, so.maxLevel);
        isMaxLevel = currentLevel >= maxLevel;
        if (tmp_skillName != null)
            tmp_skillName.SetText(so.skillName);
        if (tmp_level != null)
            tmp_level.SetText(isMaxLevel ?
                $"<size=75%>Lv.</size>{"MAX"}" :
                $"<size=75%>Lv.</size>{currentLevel} <size=75%> / {maxLevel}</size>");
        if (tmp_description != null)
            tmp_description.SetText(FormatDescription(so, currentLevel, isMaxLevel));

        SetParamValues(so, currentLevel, isMaxLevel);
        SetData_RequiredCost(currentLevel);

        if (obj_complete != null)
            obj_complete.SetActive(isMaxLevel);
        if (obj_resourceRoot != null)
            obj_resourceRoot.SetActive(!isMaxLevel);
        gameObject.SetActive(true);
    }

    static string FormatDescription(SkillTreeBase skill, int currentLevel, bool isMaxLevel)
    {
        if (string.IsNullOrEmpty(skill.description)) return "";
        int paramLevel = isMaxLevel ? currentLevel : currentLevel + 1;
        return skill.description.Replace("[A]", FormatParams(skill, paramLevel));
    }

    static string FormatParams(SkillTreeBase skill, int atLevel)
    {
        float value = skill.deltaValue * atLevel;
        if (!HasDisplayUnit(skill.unit))
        {
            if (IsAttackDamage(skill))
                return PlainSigned(value);
            return "";
        }
        return FormatParam(skill.unit, value);
    }

    void SetParamValues(SkillTreeBase skill, int currentLevel, bool maxed)
    {
        bool showValue = HasDisplayUnit(skill.unit) || IsAttackDamage(skill);
        if (obj_param != null)
            obj_param.SetActive(showValue);

        bool showNext = showValue && !maxed;
        if (tmp_paramNow != null)
        {
            tmp_paramNow.gameObject.SetActive(showValue);
            if (showValue)
                tmp_paramNow.SetText(FormatParamDisplay(skill, currentLevel));
        }
        if (tmp_paramNext != null)
        {
            tmp_paramNext.gameObject.SetActive(showNext);
            if (showNext)
                tmp_paramNext.SetText(FormatParamDisplay(skill, currentLevel + 1));
        }
        if (obj_paramArrow != null)
            obj_paramArrow.SetActive(showNext);
    }

    string FormatParamDisplay(SkillTreeBase skill, int atLevel)
    {
        if (skill.paramStat == SkillParamStat.AttackInterval)
            return $"{AttackIntervalAt(skill, atLevel).ToString("F2")} sec";
        if (IsAttackDamage(skill))
            return AttackDamageAt(skill, atLevel).ToString();
        return FormatParams(skill, atLevel);
    }

    static bool IsAttackDamage(SkillTreeBase skill)
    {
        return skill.paramTarget == SkillParamTarget.Attack && skill.paramStat == SkillParamStat.Damage;
    }

    int AttackDamageAt(SkillTreeBase skill, int atLevel)
    {
        float bonus = SkillTreeBonus.AttackValue(skill.targetIndex, SkillParamStat.Damage);
        int appliedLevel = currentUnit != null ? currentUnit.level : 0;
        bonus += skill.deltaValue * (atLevel - appliedLevel);

        float baseDamage = 0f;
        var attacks = SOLoader.AttackUnitData;
        var attack = attacks != null ? attacks.Find(skill.targetIndex) : null;
        if (attack != null)
            baseDamage = attack.baseDamage;

        return SkillTreeBonus.RoundDamage(baseDamage + bonus);
    }

    float AttackIntervalAt(SkillTreeBase skill, int atLevel)
    {
        float bonus = SkillTreeBonus.CursorAttackInterval;
        int appliedLevel = currentUnit != null ? currentUnit.level : 0;
        bonus += skill.deltaValue * (atLevel - appliedLevel);

        float baseInterval = 0f;
        var cursor = SOLoader.CursorUnitData != null ? SOLoader.CursorUnitData.GetCursorUnitData(0) : null;
        if (cursor != null)
            baseInterval = cursor.attackInterval;

        float withSkill = Mathf.Max(0.05f, baseInterval + bonus);
        return Mathf.Max(0.05f, withSkill + MilestoneBonus.AttackIntervalAdd);
    }

    static bool HasDisplayUnit(string unit)
    {
        return !string.IsNullOrEmpty(unit) && unit != "NA";
    }

    static string FormatParam(string unit, float value)
    {
        switch (unit)
        {
            case "%":
                return $"{Signed(value * 100f, "F1")} %";
            case "Sec":
                return $"{Signed(value, "F1")} sec";
            case "Count":
                return Signed(value, "F0");
            case "NA":
            case "":
            case null:
                return "";
            default:
                return $"{Signed(value, "F1")} {unit}";
        }
    }

    static string PlainSigned(float value)
    {
        bool whole = Mathf.Abs(value - Mathf.Round(value)) < 0.001f;
        return Signed(value, whole ? "F0" : "F1");
    }

    static string Signed(float value, string format)
    {
        string number = Mathf.Abs(value).ToString(format);
        return value < 0f ? "-" + number : "+" + number;
    }

    private void EnsureCachedRefs()
    {
        if (rectTr == null) rectTr = transform as RectTransform;
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
    }

    private void ApplyVerticalOffset(float offsetY)
    {
        var targetScreenPos = RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), anchorWorldPosition);
        targetScreenPos.y += offsetY;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rectTr.parent as RectTransform,
            targetScreenPos,
            GetCanvasCamera(),
            out var worldPos
        );
        rectTr.position = worldPos;
    }

    private Camera GetCanvasCamera()
    {
        if (rootCanvas == null) return null;
        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }

    private bool IsAnyCornerOutOfScreen()
    {
        EnsureCachedRefs();
        var corners = new Vector3[4];
        rectTr.GetWorldCorners(corners);
        var cam = GetCanvasCamera();
        for (var i = 0; i < corners.Length; i++)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            if (screenPoint.x < 0f || screenPoint.x > Screen.width || screenPoint.y < 0f || screenPoint.y > Screen.height)
            {
                return true;
            }
        }
        return false;
    }

    public void SetData_Enhanced(int currentLevel)
    {
        SetData_Base(currentLevel);
    }

    private void SetData_RequiredCost(int currentLevel)
    {
        requredResources.Clear();
        if (ui_resourceCounts != null)
        {
            foreach (var cont in ui_resourceCounts)
            {
                if (cont != null) cont.gameObject.SetActive(false);
            }
        }

        if (currentUnit == null || isMaxLevel)
        {
            resourceReady = false;
            return;
        }

        var table = SOLoader.SkillTreeData;
        var costRow = table != null
            ? table.GetSkillTreeUnitCost(currentUnit.skillIndex, currentLevel + 1)
            : null;
        if (costRow == null)
        {
            resourceReady = false;
            return;
        }

        var costs = costRow.CopyPositiveCosts();
        requredResources.AddRange(costs);
        resourceReady = StaticManager.IsResourceEnough(costs);

        for (int i = 0; i < costs.Length; i++)
        {
            var resource = costs[i];
            var cont = FindResourceCounter(resource.resourceType);
            if (cont == null) continue;

            var IsResourceEnough = StaticManager.IsResourceEnough(resource.resourceType, resource.requiredCount);
            cont.gameObject.SetActive(true);
            cont.SetCount(resource.requiredCount, IsResourceEnough);
        }
    }

    UI_ResourceCounter_Simple FindResourceCounter(ResourceType resourceType)
    {
        if (ui_resourceCounts == null) return null;
        for (int i = 0; i < ui_resourceCounts.Length; i++)
        {
            var cont = ui_resourceCounts[i];
            if (cont != null && cont.ResourceType == resourceType)
                return cont;
        }
        return null;
    }
}
