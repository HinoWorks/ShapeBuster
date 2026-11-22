using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum GameLanguage
{
    Ja = 0,
    En = 1,
}

/// <summary>
/// 表示文言の1件。シート Locale の列名とフィールド名を揃える。
/// key は画面やデータのID。ja と en はそれぞれの文。書式は {0} を使う。
/// </summary>
[Serializable]
public class LocaleEntry
{
    public string key;
    public string ja;
    public string en;
}

[CreateAssetMenu(menuName = "SO/Locale")]
public class SO_Locale : ScriptableObject
{
    public TMP_FontAsset fontJa;
    public TMP_FontAsset fontEn;
    public LocaleEntry[] entries;

    /// <summary>
    /// シートの行でまるごと置き換える（Locale はシートが正本）。シートに無い行は消える。
    /// 同じ key が何度もあるときは最初の行を使う。key が空の行は飛ばす。空欄の ja・en は空文字にする。
    /// </summary>
    public void Replace(LocaleEntry[] loaded)
    {
        if (loaded == null || loaded.Length == 0) return;

        var list = new List<LocaleEntry>();
        var seen = new HashSet<string>();
        for (int i = 0; i < loaded.Length; i++)
        {
            var row = loaded[i];
            if (row == null || string.IsNullOrEmpty(row.key) || !seen.Add(row.key)) continue;
            row.ja = row.ja ?? "";
            row.en = row.en ?? "";
            list.Add(row);
        }
        entries = list.ToArray();
    }

    /// <summary>今のアセットにあって、loaded に無い key。Replace すると消える行。</summary>
    public List<string> KeysMissingIn(LocaleEntry[] loaded)
    {
        var keep = new HashSet<string>();
        if (loaded != null)
        {
            foreach (var row in loaded)
                if (row != null && !string.IsNullOrEmpty(row.key)) keep.Add(row.key);
        }

        var missing = new List<string>();
        if (entries == null) return missing;
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.key) && !keep.Contains(e.key)) missing.Add(e.key);
        return missing;
    }
}
