using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UI_SkillTreeDetail : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_skillName;
    [SerializeField] TextMeshProUGUI tmp_description;
    [SerializeField] TextMeshProUGUI tmp_level;
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

        SetData_RequiredCost();

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
        var lines = new List<string>();
        var primary = FormatParam(skill.unit, skill.deltaValue * atLevel);
        if (!string.IsNullOrEmpty(primary))
            lines.Add(primary);

        if (skill.deltaValue2 != 0f)
        {
            var secondaryUnit = string.IsNullOrEmpty(skill.unit2) ? "" : skill.unit2;
            var secondary = FormatParam(secondaryUnit, skill.deltaValue2 * atLevel);
            if (string.IsNullOrEmpty(secondary))
                secondary = $"+{(skill.deltaValue2 * atLevel).ToString("F1")}";
            lines.Add(secondary);
        }
        return string.Join("\n", lines);
    }

    static string FormatParam(string unit, float value)
    {
        switch (unit)
        {
            case "%":
                return $"+{(value * 100f).ToString("F1")} %";
            case "Sec":
                return $"+{value.ToString("F1")} sec";
            case "Count":
                return $"+{value.ToString("F0")}";
            case "NA":
            case "":
            case null:
                return "";
            default:
                return $"+{value.ToString("F1")} {unit}";
        }
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

    private void SetData_RequiredCost()
    {
        requredResources.Clear();
        if (ui_resourceCounts != null)
        {
            foreach (var cont in ui_resourceCounts)
            {
                if (cont != null) cont.gameObject.SetActive(false);
            }
        }

        if (currentUnit == null || currentUnit.skillTreeUnit == null)
        {
            resourceReady = false;
            return;
        }

        var costs = currentUnit.skillTreeUnit.CopyPositiveCosts();
        requredResources.AddRange(costs);
        resourceReady = StaticManager.IsResourceEnough(costs);

        for (int i = 0; i < costs.Length; i++)
        {
            var resource = costs[i];
            var cont = FindResourceCounter(resource.resourceType);
            if (cont == null) continue;

            cont.gameObject.SetActive(true);
            cont.SetCount(resource.requiredCount);
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
