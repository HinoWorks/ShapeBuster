using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// オプションの 1 行。スライダーで 0〜100% を決める（音量）。右に今の % を出す。
/// </summary>
public class UI_OptionSlider : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_label;
    [SerializeField] Slider slider;
    [SerializeField] TextMeshProUGUI tmp_value;

    Func<float> getter;
    Action<float> setter;
    bool silent;

    /// <summary>getter と setter は 0〜1 で受け渡す。</summary>
    public void Setup(Func<float> getter, Action<float> setter)
    {
        this.getter = getter;
        this.setter = setter;
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(OnValueChanged);
        }
        Refresh();
    }

    void OnValueChanged(float value)
    {
        if (silent) return;
        // 5% 刻みにそろえる。
        value = Mathf.Round(value * 20f) / 20f;
        setter?.Invoke(value);
        ShowValue(value);
    }

    public void Refresh()
    {
        if (getter == null) return;
        float value = getter();
        if (slider != null)
        {
            silent = true;
            slider.value = value;
            silent = false;
        }
        ShowValue(value);
    }

    void ShowValue(float value)
    {
        if (tmp_value == null) return;
        tmp_value.SetText(Mathf.RoundToInt(value * 100f) + "%");
    }
}
