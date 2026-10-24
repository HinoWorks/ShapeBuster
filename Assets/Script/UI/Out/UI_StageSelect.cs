using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アウトゲームの Start から開くステージ選択。クリア済みの次のステージまで選べる。
/// </summary>
public class UI_StageSelect : MonoBehaviour
{
    readonly List<int> stageIndexes = new();

    [SerializeField] TextMeshProUGUI tmp_stageLevel;
    [SerializeField] HButton button_prev;
    [SerializeField] HButton button_next;
    [SerializeField] HButton button_start;
    [SerializeField] Button button_close;

    int selectedStageIndex;


    void Awake()
    {
        ResolveMissing();
        button_prev?.onClick.AddListener(OnClick_Prev);
        button_next?.onClick.AddListener(OnClick_Next);
        button_start?.onClick.AddListener(OnClick_Start);
        button_close?.onClick.AddListener(Close);
    }

    public void Open()
    {
        CollectStageIndexes();
        selectedStageIndex = HighestSelectable();
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }


    public void OnClick_Prev()
    {
        int index = IndexOf(selectedStageIndex);
        if (index <= 0) return;
        selectedStageIndex = stageIndexes[index - 1];
        Refresh();
    }

    public void OnClick_Next()
    {
        int index = IndexOf(selectedStageIndex);
        if (index < 0 || index + 1 >= stageIndexes.Count) return;

        int next = stageIndexes[index + 1];
        if (!CanSelect(next)) return;
        selectedStageIndex = next;
        Refresh();
    }

    public void OnClick_Start()
    {
        if (!CanSelect(selectedStageIndex)) return;
        if (StageTimeManager.Inst == null)
        {
            Debug.LogWarning("StageTimeManager がないため、ステージを開始できません。");
            return;
        }

        StageTimeManager.Inst.SetStageIndex(selectedStageIndex);
        Close();
        GameWatcher.Inst.SetGameState(GameStateType.InGame_Ready);
        Debug.Log($"<color=yellow> ========= Stage Select: {selectedStageIndex} ========= </color>");
    }


    void Refresh()
    {
        if (tmp_stageLevel != null)
            tmp_stageLevel.SetText(stageIndexes.Count == 0 ? "-" : selectedStageIndex.ToString());

        int index = IndexOf(selectedStageIndex);
        bool hasStage = index >= 0;
        SetInteractable(button_prev, hasStage && index > 0);
        SetInteractable(button_next, hasStage && index + 1 < stageIndexes.Count && CanSelect(stageIndexes[index + 1]));
        SetInteractable(button_start, hasStage && CanSelect(selectedStageIndex));
    }

    static void SetInteractable(HButton button, bool interactable)
    {
        if (button == null) return;
        button.Set_Interactable(interactable);
    }

    int HighestSelectable()
    {
        if (stageIndexes.Count == 0) return 0;
        if (!TryGetBounds(out _, out int maxSelectable))
            return stageIndexes[0];

        int chosen = stageIndexes[0];
        for (int i = 0; i < stageIndexes.Count; i++)
        {
            if (stageIndexes[i] <= maxSelectable)
                chosen = stageIndexes[i];
        }
        return chosen;
    }

    bool CanSelect(int stageIndex)
    {
        return TryGetBounds(out _, out int maxSelectable)
            && stageIndex <= maxSelectable
            && stageIndexes.Contains(stageIndex);
    }

    bool TryGetBounds(out int firstStageIndex, out int maxSelectable)
    {
        firstStageIndex = 0;
        maxSelectable = 0;
        if (stageIndexes.Count == 0)
            CollectStageIndexes();
        if (stageIndexes.Count == 0) return false;

        firstStageIndex = stageIndexes[0];
        maxSelectable = SaveLoader.Inst != null
            ? SaveLoader.Inst.GetMaxSelectableStageIndex(firstStageIndex)
            : firstStageIndex;
        return true;
    }

    int IndexOf(int stageIndex)
    {
        for (int i = 0; i < stageIndexes.Count; i++)
        {
            if (stageIndexes[i] == stageIndex)
                return i;
        }
        return -1;
    }

    void CollectStageIndexes()
    {
        stageIndexes.Clear();
        var data = SOLoader.StageData;
        if (data == null || data.stageDatas == null) return;

        for (int i = 0; i < data.stageDatas.Length; i++)
        {
            var stage = data.stageDatas[i];
            if (stage == null || stageIndexes.Contains(stage.stageIndex)) continue;
            stageIndexes.Add(stage.stageIndex);
        }
        stageIndexes.Sort();
    }


    void ResolveMissing()
    {
        if (tmp_stageLevel == null)
        {
            var label = transform.Find("main/tmp_stageLevel");
            if (label != null)
                tmp_stageLevel = label.GetComponent<TextMeshProUGUI>();
        }

        if (button_close == null)
        {
            var dim = transform.Find("base");
            if (dim != null)
            {
                button_close = dim.GetComponent<Button>();
                if (button_close == null)
                {
                    button_close = dim.gameObject.AddComponent<Button>();
                    button_close.transition = Selectable.Transition.None;
                    button_close.targetGraphic = dim.GetComponent<Graphic>();
                }
            }
        }

        if (button_prev != null && button_next != null && button_start != null) return;

        var buttons = GetComponentsInChildren<HButton>(true);
        if (button_start == null)
            button_start = LowestButton(buttons);

        HButton left = null;
        HButton right = null;
        float leftX = float.PositiveInfinity;
        float rightX = float.NegativeInfinity;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || buttons[i] == button_start) continue;
            var rect = buttons[i].transform as RectTransform;
            if (rect == null) continue;
            if (rect.anchoredPosition.x <= leftX)
            {
                leftX = rect.anchoredPosition.x;
                left = buttons[i];
            }
            if (rect.anchoredPosition.x >= rightX)
            {
                rightX = rect.anchoredPosition.x;
                right = buttons[i];
            }
        }

        if (button_prev == null) button_prev = left;
        if (button_next == null) button_next = right;
    }

    static HButton LowestButton(HButton[] buttons)
    {
        HButton lowest = null;
        float lowestY = float.PositiveInfinity;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            var rect = buttons[i].transform as RectTransform;
            if (rect == null) continue;
            if (rect.anchoredPosition.y >= lowestY) continue;
            lowestY = rect.anchoredPosition.y;
            lowest = buttons[i];
        }
        return lowest;
    }
}
