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
    /// シートの行を足す。同じ key はシート側で上書きする。シートに無い行は残す。
    /// </summary>
    public void Merge(LocaleEntry[] loaded)
    {
        if (loaded == null || loaded.Length == 0) return;

        var list = new List<LocaleEntry>();
        if (entries != null)
            list.AddRange(entries);

        var index = new Dictionary<string, int>();
        for (int i = 0; i < list.Count; i++)
        {
            string key = list[i] != null ? list[i].key : null;
            if (!string.IsNullOrEmpty(key) && !index.ContainsKey(key))
                index.Add(key, i);
        }

        for (int i = 0; i < loaded.Length; i++)
        {
            var row = loaded[i];
            if (row == null || string.IsNullOrEmpty(row.key)) continue;
            if (index.TryGetValue(row.key, out int at))
                list[at] = row;
            else
            {
                index.Add(row.key, list.Count);
                list.Add(row);
            }
        }

        entries = list.ToArray();
    }
}
