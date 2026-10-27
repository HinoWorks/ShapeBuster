using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using UniRx;
#if UNITY_EDITOR
using UnityEditor;
#endif


public enum SkillTreeUnlockState
{
    Hide,
    Locked,
    EnhanceReady,
    EnhanceComplete
}

public class UI_SkillTreeManager : MonoBehaviour
{
    public UI_SkillTreeUnit[] skillTreeUnits;
    [SerializeField] UI_SkillTreeDetail ui_skillTreeDetail;
    [SerializeField] GameObject headerCheckMark;

    [Space(10)]
    [Header("Node settings")]
    [SerializeField] GameObject nodeContPrefab;
    [SerializeField] List<UI_SkillTreeNodeCont> nodeConts = new List<UI_SkillTreeNodeCont>();
    [SerializeField] RectTransform nodeRoot;
    [SerializeField] float nodeLineHeight = 4f;
    [SerializeField] float neighborStep = 160f;
    [SerializeField] float neighborTolerance = 36f;


    [Space(10)]
    [Header("Scroll settings")]
    [SerializeField] RectTransform scrollContent;
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 2.0f;

    [Header("Pan")]
    [SerializeField] private float panSpeed = 1.0f;
    [SerializeField] private float panPadding = 0f;

#if UNITY_EDITOR
    [SerializeField] bool debugUpgradeWithoutCost;
#endif


    private bool haveEnhanceReadyUnit = false;

    private Vector2 lastMousePos;
    private bool panBlocked;
    private bool wired;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();

    private state currentState = state.Idling;



#if UNITY_EDITOR
    [ContextMenu("SkillTreeData_Update")]
    public void SkillTreeData_Update()
    {
        var table = SOLoader.SkillTreeData;
        if (table == null)
        {
            Debug.LogError("SO_SkillTreeData が Resources/SO にありません。");
            return;
        }

        if (nodeConts != null)
        {
            foreach (var nodeCont in nodeConts)
            {
                if (nodeCont != null)
                    nodeCont.NotActivate();
            }
        }

        skillTreeUnits = transform.GetComponentsInChildren<UI_SkillTreeUnit>(true);
        foreach (var skillTreeUnit in skillTreeUnits)
        {
            var soUnit = table.GetSkillTreeUnitData(skillTreeUnit.skillIndex);
            if (soUnit == null)
            {
                Debug.LogError($"SkillTreeUnit not found: {skillTreeUnit.skillIndex}");
                continue;
            }
            var soBase = table.GetSkillTreeBaseData(skillTreeUnit.skillIndex);
            if (soBase == null)
            {
                Debug.LogError($"SkillTreeBase not found: {skillTreeUnit.skillIndex}");
                continue;
            }
            skillTreeUnit.OnValidateCall(soBase, soUnit);
        }

        RebuildNeighborPrerequisites();
        var drawnPairs = new HashSet<(int, int)>();
        foreach (var skillTreeUnit in skillTreeUnits)
        {
            if (skillTreeUnit == null) continue;
            NodeCreate(skillTreeUnit, drawnPairs);
            EditorUtility.SetDirty(skillTreeUnit);
        }

        EditorUtility.SetDirty(this);
        Debug.Log("<color=green>=== SkillTreeData_Update end ===</color>");
    }

    private void NodeCreate(UI_SkillTreeUnit unit, HashSet<(int, int)> drawnPairs)
    {
        var neighbors = unit.NeighborSkillIndexes;
        if (neighbors == null || neighbors.Length == 0) return;

        foreach (var baseIndex in neighbors)
        {
            if (baseIndex < 0) continue;
            var baseUnit = Array.Find(skillTreeUnits, x => x != null && x.skillIndex == baseIndex);
            if (baseUnit == null || baseUnit == unit) continue;

            int idA = unit.GetInstanceID();
            int idB = baseUnit.GetInstanceID();
            var pair = idA < idB ? (idA, idB) : (idB, idA);
            if (!drawnPairs.Add(pair)) continue;

            var nodeCont = Get_FreeNodeCont();
            if (nodeCont == null) continue;
            nodeCont.SetNodeCont(baseUnit, unit, nodeLineHeight);
        }
    }

    private UI_SkillTreeNodeCont Get_FreeNodeCont()
    {
        var nodeCont = nodeConts.Find(x => x != null && x.gameObject.activeSelf == false);
        if (nodeCont == null)
        {
            if (nodeContPrefab == null || nodeRoot == null) return null;
            var newCont = PrefabUtility.InstantiatePrefab(nodeContPrefab, nodeRoot) as GameObject;
            if (newCont == null) return null;
            nodeCont = newCont.GetComponent<UI_SkillTreeNodeCont>();
            if (nodeCont == null) return null;
            nodeConts.Add(nodeCont);
        }
        return nodeCont;
    }
#endif




    public void RebuildNeighborPrerequisites()
    {
        if (skillTreeUnits == null) return;

        var board = scrollContent != null ? scrollContent : transform as RectTransform;
        var positions = new Vector2[skillTreeUnits.Length];
        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            var rect = unit != null ? unit.transform as RectTransform : null;
            if (unit == null || rect == null || board == null) continue;
            positions[i] = board.InverseTransformPoint(rect.position);
        }

        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            if (unit == null) continue;
            if (unit.IsRoot || neighborStep <= 0f)
            {
                unit.SetNeighborSkillIndexes(Array.Empty<int>());
                continue;
            }

            var found = new List<int>();
            for (int j = 0; j < skillTreeUnits.Length; j++)
            {
                if (i == j) continue;
                var other = skillTreeUnits[j];
                if (other == null) continue;
                if (!IsCardinalNeighbor(positions[i], positions[j])) continue;
                if (!found.Contains(other.skillIndex))
                    found.Add(other.skillIndex);
            }
            unit.SetNeighborSkillIndexes(found.ToArray());
        }
    }

    bool IsCardinalNeighbor(Vector2 a, Vector2 b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        bool horizontal = Mathf.Abs(dx - neighborStep) <= neighborTolerance && dy <= neighborTolerance;
        bool vertical = Mathf.Abs(dy - neighborStep) <= neighborTolerance && dx <= neighborTolerance;
        return horizontal || vertical;
    }



    public void Init_Once()
    {
        if (skillTreeUnits == null)
            skillTreeUnits = Array.Empty<UI_SkillTreeUnit>();

        foreach (var skillTreeUnit in skillTreeUnits)
        {
            if (skillTreeUnit == null) continue;
            skillTreeUnit.AwakeCall(this, OnMouseOver, OnClick_Enhance, UpdateNodeState);
        }

        if (ui_skillTreeDetail != null)
            ui_skillTreeDetail.Hide();
        if (headerCheckMark != null)
            headerCheckMark.SetActive(false);

        GameEvent.UI.ResourceMod_OutGame.Subscribe(_ => RefreshArrowsAndHeader()).AddTo(this);
        GameEvent.UI.ResourceMod.Subscribe(_ => RefreshArrowsAndHeader()).AddTo(this);

        this.gameObject.SetActive(false);
    }

    public void Init(bool _isActive)
    {
        gameObject.SetActive(_isActive);
        if (_isActive) EnterOutGame();
    }

    void EnterOutGame()
    {
        if (ui_skillTreeDetail != null)
            ui_skillTreeDetail.Hide();
        if (scrollContent != null)
        {
            DOTween.Kill(scrollContent);
            scrollContent.localScale = Vector3.one;
            scrollContent.anchoredPosition = Vector2.zero;
        }
        RefreshUnits().Forget();
    }

    public async UniTask RefreshUnits()
    {
        if (skillTreeUnits == null) return;
        RebuildNeighborPrerequisites();

        var tasks = new List<UniTask>(skillTreeUnits.Length);
        foreach (var unit in skillTreeUnits)
        {
            if (unit != null)
                tasks.Add(unit.Init());
        }
        await UniTask.WhenAll(tasks);
        RefreshArrowsAndHeader();
    }

    void RefreshArrowsAndHeader()
    {
        haveEnhanceReadyUnit = false;
        if (skillTreeUnits != null)
        {
            foreach (var unit in skillTreeUnits)
            {
                if (unit != null && unit.Set_UpgradeVector())
                    haveEnhanceReadyUnit = true;
            }
        }

        if (headerCheckMark != null)
            headerCheckMark.SetActive(haveEnhanceReadyUnit);
    }

    public bool IsResourceEnough(UI_SkillTreeUnit unit)
    {
        if (unit == null) return false;
        var table = SOLoader.SkillTreeData;
        var costRow = table != null ? table.GetSkillTreeUnitCost(unit.skillIndex, unit.level + 1) : null;
        if (costRow == null) return false;
        return StaticManager.IsResourceEnough(costRow.CopyPositiveCosts());
    }




    #region -- update 主にズームと画面スクロール用 --
    void Update()
    {
        if (!IsTreeInteractive()) return;
        HandleZoom();
        HandlePan();
        Check_PositionReset();
    }

    bool IsTreeInteractive()
    {
        var canvas = GetComponentInParent<Canvas>();
        return canvas == null || canvas.enabled;
    }

    Camera UiCamera()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return canvas.worldCamera;
    }

    bool PointerOverView()
    {
        if (scrollContent == null || Mouse.current == null) return false;
        var view = scrollContent.parent as RectTransform;
        if (view == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(view, Mouse.current.position.ReadValue(), UiCamera());
    }

    bool PointerHitsSkillUnit()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        pointerHits.Clear();
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        EventSystem.current.RaycastAll(eventData, pointerHits);
        for (int i = 0; i < pointerHits.Count; i++)
        {
            if (pointerHits[i].gameObject.GetComponentInParent<UI_SkillTreeUnit>() != null)
                return true;
        }
        return false;
    }

    void HandleZoom()
    {
        if (Mouse.current == null || scrollContent == null) return;
        if (!PointerOverView()) return;

        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            scrollContent,
            Mouse.current.position.ReadValue(),
            UiCamera(),
            out var localMousePos
        );

        float oldScale = scrollContent.localScale.x;
        float newScale = Mathf.Clamp(oldScale + wheel * zoomSpeed, minScale, maxScale);
        if (Mathf.Approximately(oldScale, newScale)) return;

        Vector2 posDelta = localMousePos * (oldScale - newScale);
        Vector2 targetPos = ClampPanPosition(scrollContent.anchoredPosition + posDelta, newScale);

        DOTween.Kill(scrollContent);
        scrollContent.localScale = Vector3.one * newScale;
        scrollContent.anchoredPosition = targetPos;
    }

    void HandlePan()
    {
        if (Mouse.current == null || scrollContent == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            lastMousePos = Mouse.current.position.ReadValue();
            panBlocked = PointerHitsSkillUnit();
        }

        if (!Mouse.current.leftButton.isPressed)
        {
            panBlocked = false;
            return;
        }

        if (panBlocked || !PointerOverView()) return;

        Vector2 current = Mouse.current.position.ReadValue();
        Vector2 delta = current - lastMousePos;
        DOTween.Kill(scrollContent);
        scrollContent.anchoredPosition = ClampPanPosition(scrollContent.anchoredPosition + delta * panSpeed);
        lastMousePos = current;
    }


    void Check_PositionReset()
    {
        if (scrollContent == null || Keyboard.current == null) return;
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;
        if (!PointerOverView()) return;

        DOTween.Kill(scrollContent);
        DOTween.To(() => scrollContent.anchoredPosition, x => scrollContent.anchoredPosition = x, Vector2.zero, 0.2f)
            .SetTarget(scrollContent)
            .SetUpdate(true);
    }

    Vector2 ClampPanPosition(Vector2 pos, float? scaleOverride = null)
    {
        var view = (RectTransform)scrollContent.parent;
        float scale = scaleOverride ?? scrollContent.localScale.x;

        float overflowX = (scrollContent.rect.width * scale - view.rect.width) * 0.5f + panPadding;
        float overflowY = (scrollContent.rect.height * scale - view.rect.height) * 0.5f + panPadding;

        float minX = overflowX > 0f ? -overflowX : 0f;
        float maxX = overflowX > 0f ? overflowX : 0f;
        float minY = overflowY > 0f ? -overflowY : 0f;
        float maxY = overflowY > 0f ? overflowY : 0f;

        return new Vector2(
            Mathf.Clamp(pos.x, minX, maxX),
            Mathf.Clamp(pos.y, minY, maxY)
        );
    }
    #endregion

    private void OnMouseOver(bool isEnter, UI_SkillTreeUnit skillTreeUnit)
    {
        if (ui_skillTreeDetail == null) return;
        if (isEnter)
        {
            ui_skillTreeDetail.SetData(skillTreeUnit);
            ui_skillTreeDetail.SetPositionWithAutoFlip(skillTreeUnit.transform.position);
        }
        else if (ui_skillTreeDetail.IsShowing(skillTreeUnit))
        {
            ui_skillTreeDetail.Hide();
        }
    }


    private async void OnClick_Enhance(UI_SkillTreeUnit skillTreeUnit)
    {
        if (currentState != state.Idling) return;
        if (skillTreeUnit == null || skillTreeUnit.skillTree == null || skillTreeUnit.skillTreeUnit == null) return;
        if (skillTreeUnit.unlockState != SkillTreeUnlockState.EnhanceReady) return;
        if (skillTreeUnit.level >= Mathf.Max(1, skillTreeUnit.skillTree.maxLevel)) return;

        var table = SOLoader.SkillTreeData;
        var costRow = table != null
            ? table.GetSkillTreeUnitCost(skillTreeUnit.skillIndex, skillTreeUnit.level + 1)
            : null;
        if (costRow == null) return;

        var costs = costRow.CopyPositiveCosts();
        bool skipCost = false;
#if UNITY_EDITOR
        skipCost = debugUpgradeWithoutCost;
#endif
        if (!skipCost && !StaticManager.IsResourceEnough(costs)) return;

        currentState = state.Doing;
        bool spent = false;
        try
        {
            if (!skipCost)
            {
                ChangeResources(costs, -1);
                spent = true;
            }

            var newLevel = skillTreeUnit.level + 1;
            SaveLoader.Inst.Request_SaveSkillTreeData(skillTreeUnit.skillIndex, newLevel);
            var saved = await SaveLoader.Inst.Get_SkillTreeData(skillTreeUnit.skillIndex);
            if (saved == null || saved.level != newLevel)
            {
                if (spent)
                    ChangeResources(costs, 1);
                return;
            }

            var isLevelMax = newLevel >= Mathf.Max(1, skillTreeUnit.skillTree.maxLevel);
            if (SoundManager.Inst != null)
                SoundManager.Inst.PlaySE(isLevelMax ? 127 : 120);

            await skillTreeUnit.CallBack_Enhance();
            if (ui_skillTreeDetail != null && ui_skillTreeDetail.IsShowing(skillTreeUnit))
                ui_skillTreeDetail.SetData_Enhanced(newLevel);

            if (newLevel == 1)
                GameEvent.AchieveEvent.PublishSkillTreeUnlock();

            if (skillTreeUnits != null)
            {
                foreach (var unit in skillTreeUnits)
                {
                    if (unit == null || unit == skillTreeUnit) continue;
                    if (unit.Requires(skillTreeUnit.skillIndex))
                        await unit.Init();
                }
            }

            RefreshArrowsAndHeader();
            GameEvent.UI.PublishResourceMod_OutGame();
        }
        finally
        {
            currentState = state.Idling;
        }
    }

    static void ChangeResources(ResourceCount[] costs, int sign)
    {
        if (costs == null || SaveLoader.Inst == null) return;
        for (int i = 0; i < costs.Length; i++)
        {
            var resource = costs[i];
            if (resource == null || resource.requiredCount <= 0) continue;
            SaveLoader.Inst.Request_SaveResource(resource.resourceType, sign * resource.requiredCount);
        }
    }

    private void UpdateNodeState(int skillIndex, SkillTreeUnlockState unlockState, int level)
    {
        if (nodeConts == null) return;

        foreach (var node in nodeConts)
        {
            if (node == null || node.TargetSkillIndexes == null) continue;
            if (!Array.Exists(node.TargetSkillIndexes, idx => idx == skillIndex)) continue;

            var unlockStateRef = SkillTreeUnlockState.Hide;
            var levelRef = 0;
            foreach (var targetIndex in node.TargetSkillIndexes)
            {
                if (targetIndex == skillIndex) continue;
                var targetRefUnit = Array.Find(skillTreeUnits, x => x != null && x.skillIndex == targetIndex);
                if (targetRefUnit == null) continue;
                unlockStateRef = targetRefUnit.unlockState;
                levelRef = targetRefUnit.level;
            }
            node.Set_LineState(unlockState, level, unlockStateRef, levelRef);
        }
    }
}
