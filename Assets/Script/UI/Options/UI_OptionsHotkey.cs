using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Esc キーでオプションを開く。UI_Options はシーンで非表示にして置くので Update が動かない。代わりに、いつも表示されている親の Canvas（Canvas_Option）にこれを付ける。
/// 開いている間の Esc（閉じる・確認を閉じる）は UI_Options 自身が受け持つ。
/// </summary>
public class UI_OptionsHotkey : MonoBehaviour
{
    [SerializeField] UI_Options options;

    void Update()
    {
        if (options == null || options.IsOpen) return;
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        options.Open();
    }
}
