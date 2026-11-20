using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゾンビ図鑑の一覧に並べるカード 1 枚。絵・名前・研究 Lv と、「NEW」「研究できる」「選択中」の印を出す。
/// 中身は UI_BestiaryManager が Setup と Refresh で入れる。
/// </summary>
public class UI_BestiaryCard : MonoBehaviour
{
    [SerializeField] HButton button;
    [SerializeField] Image image_portrait;
    [SerializeField] TextMeshProUGUI tmp_name;
    [SerializeField] TextMeshProUGUI tmp_level;
    [Tooltip("発見して、まだ詳細を開いていないときに出す")]
    [SerializeField] GameObject obj_new;
    [Tooltip("次の Lv を研究できるときに出す")]
    [SerializeField] GameObject obj_ready;
    [Tooltip("選んでいるカードに出す枠")]
    [SerializeField] GameObject obj_selected;

    // 未発見のときの絵の色（シルエット）。
    static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.85f);

    public int Id { get; private set; }
    Action<int> onSelect;

    public void Setup(int id, Action<int> select)
    {
        Id = id;
        onSelect = select;
        if (button == null)
            button = GetComponent<HButton>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(Id));
        }
        if (image_portrait != null)
        {
            image_portrait.sprite = UI_BestiaryManager.Portrait(id);
            image_portrait.preserveAspect = true;
            image_portrait.enabled = image_portrait.sprite != null;
        }
    }

    public void Refresh(bool selected)
    {
        var data = BestiaryRuntime.Data(Id);
        var state = BestiaryRuntime.Get(Id);
        bool found = state.found;

        if (image_portrait != null)
            image_portrait.color = found ? Color.white : Silhouette;
        if (tmp_name != null)
        {
            Loc.UseFont(tmp_name);
            tmp_name.SetText(found ? UI_BestiaryManager.NameOf(Id) : "???");
        }
        if (tmp_level != null)
        {
            bool showLevel = found && data != null && !data.boss && data.MaxLevel > 0;
            tmp_level.gameObject.SetActive(showLevel);
            if (showLevel)
                tmp_level.SetText(state.level >= data.MaxLevel ? "Lv MAX" : $"Lv {state.level}/{data.MaxLevel}");
        }
        if (obj_new != null)
            obj_new.SetActive(found && !state.seen);
        if (obj_ready != null)
        {
            var block = BestiaryRuntime.CheckResearch(Id);
            obj_ready.SetActive(block == BestiaryResearchBlock.None);
        }
        if (obj_selected != null)
            obj_selected.SetActive(selected);
    }
}
