using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マイルストーン一覧。カタログの件数に合わせてユニットを並べ、表示時に進捗を読み直す。
/// </summary>
public class UI_MileStoneManager : MonoBehaviour
{
    [SerializeField] RectTransform content;
    [SerializeField] UI_MileStoneUnit unitPrefab;
    [SerializeField] ScrollRect scroll;

    UI_MileStoneUnit[] units = System.Array.Empty<UI_MileStoneUnit>();
    bool inited;

    public void Init_Once()
    {
        if (inited) return;
        inited = true;

        Resolve();
        RefreshUnits();
    }

    public void Init(bool isActive)
    {
        gameObject.SetActive(isActive);
        if (isActive)
            Enter();
    }

    public void RefreshUnits()
    {
        if (!inited)
            Init_Once();

        Resolve();
        var defs = MilestoneCatalog.All;
        EnsureCount(defs.Count);

        for (int i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            if (unit == null) continue;

            bool show = i < defs.Count;
            unit.gameObject.SetActive(show);
            if (show)
                unit.SetMilestone(defs[i].id);
        }

        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    void Awake()
    {
        Init_Once();
    }

    void Enter()
    {
        RefreshUnits();
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
    }

    void Resolve()
    {
        if (content == null)
        {
            var layout = GetComponentInChildren<VerticalLayoutGroup>(true);
            if (layout != null)
                content = layout.transform as RectTransform;
        }

        if (scroll == null)
            scroll = GetComponentInChildren<ScrollRect>(true);

        if (units == null || units.Length == 0)
            CollectUnits();
    }

    void CollectUnits()
    {
        var found = new List<UI_MileStoneUnit>();
        if (content != null)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                var unit = content.GetChild(i).GetComponent<UI_MileStoneUnit>();
                if (unit != null)
                    found.Add(unit);
            }
        }
        units = found.ToArray();
    }

    void EnsureCount(int count)
    {
        if (units == null)
            units = System.Array.Empty<UI_MileStoneUnit>();
        if (units.Length >= count || content == null)
            return;

        var template = unitPrefab;
        if (template == null && units.Length > 0)
            template = units[0];
        if (template == null)
            return;

        var list = new List<UI_MileStoneUnit>(units);
        while (list.Count < count)
        {
            var created = Instantiate(template, content);
            created.gameObject.SetActive(true);
            list.Add(created);
        }
        units = list.ToArray();
    }
}
