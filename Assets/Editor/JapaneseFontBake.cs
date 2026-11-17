using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 日本語フォントのアトラスは 1024 で埋まっている。足りない文字は四角になるので、
/// 表示に使う文字を続きのアトラスへ焼き、ビルド時に消さない。
/// </summary>
[InitializeOnLoad]
static class JapaneseFontBake
{
    const string FontPath = "Assets/Font/MPLUSRounded1c-Medium SDF.asset";

    static bool scheduled;

    static JapaneseFontBake()
    {
        EditorApplication.delayCall += Schedule;
    }

    [MenuItem("Tools/日本語フォントを更新")]
    static void Schedule()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        scheduled = false;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Schedule();
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            return;
        }

        Bake();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        Schedule();
    }

    static void Bake()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) return;

        font.ReadFontAssetDefinition();
        bool flagsChanged = AllowExtraAtlases(font);
        string characters = CollectCharacters();
        bool added = AddMissing(font, characters);
        if (!flagsChanged && !added) return;

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log(added
            ? "日本語フォントへ、足りなかった文字を追加しました。"
            : "日本語フォントを、複数アトラスのまま保存しました。");
    }

    static bool AllowExtraAtlases(TMP_FontAsset font)
    {
        font.isMultiAtlasTexturesEnabled = true;
        var so = new SerializedObject(font);
        var multi = so.FindProperty("m_IsMultiAtlasTexturesEnabled");
        var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
        bool changed = false;
        if (multi != null && !multi.boolValue)
        {
            multi.boolValue = true;
            changed = true;
        }
        if (clear != null && clear.boolValue)
        {
            clear.boolValue = false;
            changed = true;
        }
        if (changed)
            so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    static bool AddMissing(TMP_FontAsset font, string characters)
    {
        if (string.IsNullOrEmpty(characters)) return false;
        if (font.HasCharacters(characters, out List<char> missing) && (missing == null || missing.Count == 0))
            return false;
        if (missing == null || missing.Count == 0)
            missing = new List<char>(characters);

        bool added = false;
        var chunk = new StringBuilder();
        for (int i = 0; i < missing.Count; i += 48)
        {
            chunk.Clear();
            int end = Mathf.Min(i + 48, missing.Count);
            for (int j = i; j < end; j++)
                chunk.Append(missing[j]);

            string request = chunk.ToString();
            font.TryAddCharacters(request, out string still);
            if (still == null || still.Length < request.Length)
                added = true;
            if (!string.IsNullOrEmpty(still))
                Debug.LogWarning("日本語フォントに入らなかった文字: " + still, font);
        }
        return added;
    }

    static string CollectCharacters()
    {
        var set = new HashSet<char>();
        for (int i = 32; i <= 126; i++)
            set.Add((char)i);
        AddRange(set, 0x3000, 0x303F);
        AddRange(set, 0x3040, 0x30FF);
        AddRange(set, 0xFF00, 0xFFEF);
        set.Add('\u00D7');
        set.Add('\u2192');

        ScanFolder("Assets/Resources", set);
        ScanFolder("Assets/Prefab", set);
        ScanFolder("GssPaste", set);

        var text = new StringBuilder(set.Count);
        foreach (char c in set)
        {
            if (char.IsControl(c)) continue;
            text.Append(c);
        }
        return text.ToString();
    }

    static void AddRange(HashSet<char> set, int from, int to)
    {
        for (int i = from; i <= to; i++)
            set.Add((char)i);
    }

    static void ScanFolder(string folder, HashSet<char> set)
    {
        if (!Directory.Exists(folder)) return;
        string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            if (path.EndsWith(".meta")) continue;
            string ext = Path.GetExtension(path);
            if (ext != ".asset" && ext != ".prefab" && ext != ".tsv" && ext != ".txt" && ext != ".cs")
                continue;
            if (new FileInfo(path).Length > 8000000) continue;
            AddText(File.ReadAllText(path), set);
        }
    }

    static void AddText(string text, HashSet<char> set)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 5 < text.Length && text[i + 1] == 'u'
                && int.TryParse(text.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)
                && code >= 0 && code <= 0xFFFF)
            {
                set.Add((char)code);
                i += 5;
                continue;
            }

            if (text[i] > 127)
                set.Add(text[i]);
        }
    }
}
