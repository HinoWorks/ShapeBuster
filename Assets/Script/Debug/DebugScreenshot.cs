#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// エディタ専用。デバッグメニューの「1キーでスクショ」を ON にすると、1 キーでゲーム画面を保存する。
/// 保存先はプロジェクト直下の Screenshots/（Assets の外なので、Unity には読み込まれない）。
/// ON/OFF はエディタに覚えさせるので、次の再生でもそのまま。
/// </summary>
public class DebugScreenshot : MonoBehaviour
{
    const string PrefKey = "ShapeBuster_DebugScreenshot";
    const string Folder = "Screenshots";

    static DebugScreenshot inst;

    public static bool Enabled
    {
        get => UnityEditor.EditorPrefs.GetBool(PrefKey, false);
        set
        {
            UnityEditor.EditorPrefs.SetBool(PrefKey, value);
            if (value) Ensure();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        inst = null;
        if (Enabled) Ensure();
    }

    static void Ensure()
    {
        if (inst != null || !Application.isPlaying) return;
        var go = new GameObject("DebugScreenshot");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<DebugScreenshot>();
    }

    void Update()
    {
        if (!Enabled) return;
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.digit1Key.wasPressedThisFrame) return;
        Capture();
    }

    static void Capture()
    {
        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), Folder);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        // ゲームビューの解像度のまま保存する。書き出しはフレームの終わりに行われる。
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"スクショを保存しました：{path}");
    }
}
#endif
