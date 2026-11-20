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
    [Tooltip("研究 Lv の表示のまとまり（背景と tmp_level）。Lv を出さないとき（未発見・ボス）はまとめて隠す")]
    [SerializeField] GameObject obj_level;
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

    /// <summary>Lv の表示を出すか隠す。obj_level が無いプレハブでは tmp_level だけを切り替える。</summary>
    void SetLevelVisible(bool visible)
    {
        var target = obj_level != null ? obj_level : (tmp_level != null ? tmp_level.gameObject : null);
        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
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
        bool showLevel = found && data != null && !data.boss && data.MaxLevel > 0;
        SetLevelVisible(showLevel);
        if (showLevel && tmp_level != null)
            tmp_level.SetText(state.level >= data.MaxLevel ? "Lv.MAX" : $"Lv.{state.level}/{data.MaxLevel}");
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

#if UNITY_EDITOR
    /// <summary>
    /// 編集用。data の敵を見つけて Lv まで研究した見た目の文と絵を入れる。data が null なら未発見（???）。
    /// 印（NEW・研究できる・選択中）は表示を切り替えない。
    /// </summary>
    public void EditorPreview(BestiaryData data, int level)
    {
        var targets = new UnityEngine.Object[] { image_portrait, tmp_name, tmp_level };
        foreach (var target in targets)
            if (target != null) UnityEditor.Undo.RecordObject(target, "図鑑のダミー");

        bool found = data != null;
        if (image_portrait != null)
        {
            image_portrait.sprite = found ? UI_BestiaryManager.Portrait(data.id) : null;
            image_portrait.preserveAspect = true;
            image_portrait.color = found ? Color.white : Silhouette;
        }
        if (tmp_name != null)
        {
            Loc.UseFont(tmp_name);
            tmp_name.text = found ? UI_BestiaryManager.NameOf(data.id) : "???";
        }
        bool showLevel = found && !data.boss && data.MaxLevel > 0;
        var levelTarget = obj_level != null ? obj_level : (tmp_level != null ? tmp_level.gameObject : null);
        if (levelTarget != null)
        {
            UnityEditor.Undo.RecordObject(levelTarget, "図鑑のダミー");
            levelTarget.SetActive(showLevel);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(levelTarget);
        }
        if (tmp_level != null && showLevel)
            tmp_level.text = level >= data.MaxLevel ? "Lv.MAX" : $"Lv.{level}/{data.MaxLevel}";

        foreach (var target in targets)
        {
            if (target == null) continue;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            UnityEditor.EditorUtility.SetDirty(target);
        }
    }
#endif
}
