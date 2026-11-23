using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 発射できるボール数と合計を、カーソル位置に出す。
/// 発射できない場所（ブロックの上、発射エリアより上）で F を押したら、tmp_notLaunch を点滅させて知らせる。
/// </summary>
public class UI_BlockCursorInfo : MonoBehaviour
{
    [SerializeField] Image imageCNT;
    [SerializeField] RectTransform rect;
    [SerializeField] TextMeshProUGUI label;
    [Tooltip("発射できない場所で F を押したときに点滅させる文")]
    [SerializeField] TextMeshProUGUI tmp_notLaunch;

    public static readonly Color BlockedColor = new Color(1f, 0.28f, 0.28f, 1f);

    int shownLaunch = -1;
    int shownTotal = -1;

    const float NotLaunchDuration = 0.9f;
    const int NotLaunchBlinks = 3;
    /// <summary>点滅を始めてからの秒数。-1 は出していない。</summary>
    float notLaunchTime = -1f;

    void Awake()
    {
        if (rect == null)
            rect = transform as RectTransform;
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
        StopNotLaunch();
    }

    /// <summary>発射できない場所で F を押した。文を点滅させて、少したったら消す。</summary>
    public void FlashNotLaunch()
    {
        if (tmp_notLaunch == null) return;
        notLaunchTime = 0f;
        tmp_notLaunch.gameObject.SetActive(true);
        TickNotLaunch();
    }

    void Update()
    {
        if (notLaunchTime >= 0f)
        {
            notLaunchTime += Time.unscaledDeltaTime;
            TickNotLaunch();
        }
    }

    void TickNotLaunch()
    {
        if (tmp_notLaunch == null) return;
        if (notLaunchTime >= NotLaunchDuration)
        {
            StopNotLaunch();
            return;
        }
        // 点いている時間を長めにして、NotLaunchBlinks 回点滅させる。
        float phase = Mathf.Repeat(notLaunchTime / NotLaunchDuration * NotLaunchBlinks, 1f);
        tmp_notLaunch.enabled = phase < 0.65f;
    }

    void StopNotLaunch()
    {
        notLaunchTime = -1f;
        if (tmp_notLaunch != null)
            tmp_notLaunch.gameObject.SetActive(false);
    }

    public void Show(int launchable, int total, Vector2 screen, bool cannotFire, bool noBalls)
    {
        if (label == null || rect == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        if (imageCNT != null)
            imageCNT.color = cannotFire ? BlockedColor : Color.white;
        label.color = noBalls ? BlockedColor : Color.white;
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
        StopNotLaunch();
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
