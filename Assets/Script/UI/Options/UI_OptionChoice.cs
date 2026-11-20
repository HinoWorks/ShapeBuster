using System;
using TMPro;
using UnityEngine;

/// <summary>
/// オプションの 1 行。「＜ 値 ＞」の形で、左右のボタンで選択肢を切り替える。オン／オフもこの形で出す。
/// 見出し（tmp_label）は UI_LocalizedText で固定の文を出し、値（tmp_value）は UI_Options が入れる。
/// </summary>
public class UI_OptionChoice : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_label;
    [SerializeField] TextMeshProUGUI tmp_value;
    [SerializeField] HButton button_prev;
    [SerializeField] HButton button_next;

    Func<string> valueText;
    Action<int> step;

    /// <summary>valueText は今の値の文、step は左（-1）・右（+1）を押したときの処理。</summary>
    public void Setup(Func<string> valueText, Action<int> step)
    {
        this.valueText = valueText;
        this.step = step;
        if (button_prev != null)
        {
            button_prev.onClick.RemoveAllListeners();
            button_prev.onClick.AddListener(() => Step(-1));
        }
        if (button_next != null)
        {
            button_next.onClick.RemoveAllListeners();
            button_next.onClick.AddListener(() => Step(1));
        }
        Refresh();
    }

    void Step(int direction)
    {
        step?.Invoke(direction);
        Refresh();
    }

    public void Refresh()
    {
        if (tmp_value == null || valueText == null) return;
        Loc.UseFont(tmp_value);
        tmp_value.SetText(valueText());
    }
}
