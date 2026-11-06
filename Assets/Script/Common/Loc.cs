using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 表示文言を現在の言語で引く。中身は SO_Locale。言語は端末に残す。
/// </summary>
public static class Loc
{
    const string PrefKey = "key_language";

    static Dictionary<string, LocaleEntry> map;
    static bool languageLoaded;
    static GameLanguage language = GameLanguage.Ja;

    public static GameLanguage Language
    {
        get
        {
            Ensure();
            return language;
        }
    }

    public static event Action<GameLanguage> Changed;

    public static void SetLanguage(GameLanguage value)
    {
        Ensure();
        bool changed = language != value;
        language = value;
        PlayerPrefs.SetInt(PrefKey, (int)value);
        PlayerPrefs.Save();
        if (changed && Changed != null)
            Changed(value);
    }

    /// <summary>シート取り込みのあと、引き直す。</summary>
    public static void Reload()
    {
        map = null;
        Ensure();
    }

    public static bool Has(string key)
    {
        Ensure();
        return ValueOf(key) != null;
    }

    public static string Text(string key)
    {
        Ensure();
        if (string.IsNullOrEmpty(key)) return "";
        string value = ValueOf(key);
        return value != null ? value : key;
    }

    public static string Format(string key, params object[] args)
    {
        string template = Text(key);
        if (args == null || args.Length == 0) return template;
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    /// <summary>テーブルに無いときは、シートやコードに残している文を出す。</summary>
    public static string Content(string key, string fallback)
    {
        return Has(key) ? Text(key) : (fallback ?? "");
    }

    public static string SkillName(int id)
    {
        return "skill." + id + ".name";
    }

    public static string SkillDesc(int id)
    {
        return "skill." + id + ".desc";
    }

    public static string MilestoneTitle(int id)
    {
        return "milestone." + id + ".title";
    }

    public static string MilestoneBonus(int id)
    {
        return "milestone." + id + ".bonus";
    }

    /// <summary>今の言語に割り当てたフォントをラベルへ入れる。未指定なら変えない。</summary>
    public static void UseFont(TMP_Text label)
    {
        if (label == null) return;
        Ensure();
        TMP_FontAsset font = FontOf(language);
        if (font != null)
            label.font = font;
    }

    static TMP_FontAsset FontOf(GameLanguage value)
    {
        var table = SOLoader.Locale;
        if (table == null) return null;
        return value == GameLanguage.En ? table.fontEn : table.fontJa;
    }

    static void Ensure()
    {
        if (!languageLoaded)
            LoadLanguage();
        if (map == null)
            Build();
    }

    static void LoadLanguage()
    {
        languageLoaded = true;
        if (PlayerPrefs.HasKey(PrefKey))
        {
            int saved = PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.Ja);
            language = saved == (int)GameLanguage.En ? GameLanguage.En : GameLanguage.Ja;
            return;
        }

        language = Application.systemLanguage == SystemLanguage.Japanese
            ? GameLanguage.Ja
            : GameLanguage.En;
    }

    static void Build()
    {
        map = new Dictionary<string, LocaleEntry>();
        var table = SOLoader.Locale;
        var entries = table != null ? table.entries : null;
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
            map[entry.key] = entry;
        }
    }

    static string ValueOf(string key)
    {
        if (string.IsNullOrEmpty(key) || map == null) return null;
        if (!map.TryGetValue(key, out var entry) || entry == null) return null;

        string value = language == GameLanguage.En ? entry.en : entry.ja;
        if (string.IsNullOrEmpty(value))
            value = !string.IsNullOrEmpty(entry.ja) ? entry.ja : entry.en;
        if (string.IsNullOrEmpty(value)) return null;
        return value.Replace("\\n", "\n").Replace("\\t", "\t");
    }
}
