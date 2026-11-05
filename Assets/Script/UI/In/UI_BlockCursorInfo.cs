using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 発射できるボール数と合計を、カーソル位置に出す。
/// </summary>
public class UI_BlockCursorInfo : MonoBehaviour
{
    [SerializeField] Image imageCNT;
    [SerializeField] RectTransform rect;
    [SerializeField] TextMeshProUGUI label;

    int shownLaunch = -1;
    int shownTotal = -1;

    void Awake()
    {
        if (rect == null)
            rect = transform as RectTransform;
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void Show(int launchable, int total, Vector2 screen, bool blocked)
    {
        if (label == null || rect == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        if (imageCNT != null)
            imageCNT.color = blocked ? new Color(1f, 0.28f, 0.28f, 1f) : Color.white;
        if (launchable != shownLaunch || total != shownTotal)
        {
            shownLaunch = launchable;
            shownTotal = total;
            label.text = $"{launchable}/{total}";
        }

        var canvas = rect.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        var parent = rect.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var local))
            rect.anchoredPosition = local;
    }

    public void Hide()
    {
        shownLaunch = -1;
        shownTotal = -1;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
