using TMPro;
using UnityEngine;

/// <summary>
/// 右下に、発射できるボール数と所持数を出す。
/// </summary>
public class UI_BlockBallHud : MonoBehaviour
{
    TextMeshProUGUI label;
    int shownLaunch = -1;
    int shownTotal = -1;

    public static UI_BlockBallHud Create(Transform parent, TMP_FontAsset font)
    {
        var host = new GameObject("BlockBallHud", typeof(RectTransform));
        var rect = host.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(280f, 88f);

        var text = host.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.raycastTarget = false;
        text.color = Color.white;

        var hud = host.AddComponent<UI_BlockBallHud>();
        hud.label = text;
        return hud;
    }

    public void Set(int launchable, int total)
    {
        if (label == null || (launchable == shownLaunch && total == shownTotal)) return;
        shownLaunch = launchable;
        shownTotal = total;
        label.text = $"発射 {launchable}\n所持 {total}";
    }
}
