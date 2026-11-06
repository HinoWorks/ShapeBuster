using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshPro の固定文言を、現在の言語で出す。
/// key が空のときは、プレハブに書いてある文を key として引く。テーブルに無ければそのまま出す。
/// </summary>
public class UI_LocalizedText : MonoBehaviour
{
    [SerializeField] string key;

    TMP_Text label;
    string source;
    bool captured;

    void Awake()
    {
        if (label == null)
            label = GetComponent<TMP_Text>();
        Capture();
    }

    void OnEnable()
    {
        Capture();
        Loc.Changed += OnLanguage;
        Apply();
    }

    void OnDisable()
    {
        Loc.Changed -= OnLanguage;
    }

    void OnLanguage(GameLanguage _)
    {
        Apply();
    }

    void Capture()
    {
        if (captured || label == null) return;
        source = label.text;
        captured = true;
    }

    void Apply()
    {
        if (label == null) return;
        string lookup = string.IsNullOrEmpty(key) ? source : key;
        if (!Loc.Has(lookup)) return;
        Loc.UseFont(label);
        label.text = Loc.Text(lookup);
    }
}
