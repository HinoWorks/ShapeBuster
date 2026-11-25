using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アウトゲームのリソース表示（UI_ResourceCounter）にマウスを乗せたときに出す説明。
/// 名前・何なのか・どこで手に入るかを、表示の右横に小さな枠で出す。
/// プレハブは使わず、はじめて出すときに、表示のある Canvas の一番手前へコードで組み立てる。
/// 出るときのアニメはスキルの詳細（pf_ui_skillTreeDetail）と同じ animCont_UICommon を使う。
/// このアニメは子の「main」を動かすので、外側（アニメーター）の中に main（枠と文字）を入れる形にする。
/// 文言は SO_Locale の resource.<種類>.name / .desc / .source。
/// </summary>
public class UI_ResourceTooltip : MonoBehaviour
{
    const float Width = 460f;
    const float Gap = 10f;
    const float ScreenMargin = 16f;
    static readonly Color BackColor = new Color(0.07f, 0.08f, 0.11f, 0.94f);
    static readonly Color FrameColor = new Color(0.55f, 0.58f, 0.66f, 0.9f);
    static readonly Color TitleColor = new Color(0.96f, 0.95f, 0.9f, 1f);
    static readonly Color BodyColor = new Color(0.82f, 0.83f, 0.86f, 1f);
    static readonly Color SourceColor = new Color(0.62f, 0.86f, 0.7f, 1f);

    static UI_ResourceTooltip inst;

    /// <summary>出るときのアニメ。UI_OutGameManager が入れる。空ならアニメなしで出す。</summary>
    public static RuntimeAnimatorController OpenAnimation;

    RectTransform rect;
    RectTransform main;
    RectTransform canvasRect;
    TextMeshProUGUI title;
    TextMeshProUGUI body;
    TextMeshProUGUI source;
    UI_ResourceCounter owner;

    /// <summary>counter の下に、その資源の説明を出す。</summary>
    public static void Show(UI_ResourceCounter counter)
    {
        if (counter == null) return;
        var canvas = counter.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var root = canvas.rootCanvas;
        if (inst == null || inst.canvasRect != (RectTransform)root.transform)
        {
            if (inst != null) Destroy(inst.gameObject);
            inst = Create(root);
        }
        inst.Open(counter);
    }

    /// <summary>counter の説明を出しているときだけ閉じる（別の表示へ移ったときに消さないため）。</summary>
    public static void Hide(UI_ResourceCounter counter)
    {
        if (inst == null || inst.owner != counter) return;
        inst.owner = null;
        inst.gameObject.SetActive(false);
    }

    static UI_ResourceTooltip Create(Canvas root)
    {
        var go = new GameObject("ui_resourceTooltip", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root.transform, false);

        var tip = go.AddComponent<UI_ResourceTooltip>();
        tip.canvasRect = (RectTransform)root.transform;
        tip.rect = (RectTransform)go.transform;
        tip.rect.anchorMin = tip.rect.anchorMax = new Vector2(0.5f, 0.5f);
        tip.rect.pivot = new Vector2(0.5f, 0.5f);
        tip.rect.sizeDelta = Vector2.zero;
        if (OpenAnimation != null)
        {
            // 有効になるたびに、はじめから再生される（スキルの詳細と同じ）。
            var animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = OpenAnimation;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        // 中身（枠と文字）。アニメはこの main の大きさと傾きを動かすので、真ん中を基準にする。
        var body = new GameObject("main",
            typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        body.layer = go.layer;
        body.transform.SetParent(go.transform, false);
        tip.main = (RectTransform)body.transform;
        tip.main.anchorMin = tip.main.anchorMax = tip.main.pivot = new Vector2(0.5f, 0.5f);
        tip.main.sizeDelta = new Vector2(Width, 0f);

        var back = body.GetComponent<Image>();
        back.color = BackColor;
        back.raycastTarget = false;
        var frame = body.GetComponent<Outline>();
        frame.effectColor = FrameColor;
        frame.effectDistance = new Vector2(2f, -2f);

        // 説明の枠がマウスを横取りして、表示からマウスが外れたことにならないようにする。
        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        var layout = body.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 16);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = body.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tip.title = Label(body.transform, "tmp_title", 30f, TitleColor);
        tip.body = Label(body.transform, "tmp_body", 24f, BodyColor);
        tip.source = Label(body.transform, "tmp_source", 24f, SourceColor);
        go.SetActive(false);
        return tip;
    }

    static TextMeshProUGUI Label(Transform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.lineSpacing = 4f;
        return label;
    }

    void Open(UI_ResourceCounter counter)
    {
        owner = counter;
        // AA などはコードネームなので画面には出さない。使い道も書かない（何なのかと、どこで手に入るかだけ）。
        string id = counter.ResourceType.ToString();
        SetText(title, Loc.Content($"resource.{id}.name", ""));
        SetText(body, Loc.Content($"resource.{id}.desc", ""));
        SetText(source, Loc.Content($"resource.{id}.source", ""));

        gameObject.SetActive(true);
        rect.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(main);
        Place((RectTransform)counter.transform);
    }

    static void SetText(TextMeshProUGUI label, string text)
    {
        Loc.UseFont(label);
        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>
    /// 表示の右横に、上の端をそろえて置く（リソースは縦に並ぶので、下に出すとほかのリソースを隠してしまう）。
    /// 右にはみ出すなら左横に出し、下にはみ出すなら上へずらす。
    /// </summary>
    void Place(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 leftTop = canvasRect.InverseTransformPoint(corners[1]);
        Vector2 rightTop = canvasRect.InverseTransformPoint(corners[2]);

        var area = canvasRect.rect;
        float width = main.rect.width;
        float height = main.rect.height;

        // 枠の上の端と、左右どちらかの端を決めてから、真ん中（アニメの基準）の位置に直して置く。
        bool right = rightTop.x + Gap + width <= area.xMax - ScreenMargin;
        float left = right ? rightTop.x + Gap : leftTop.x - Gap - width;
        float top = Mathf.Max(rightTop.y, area.yMin + ScreenMargin + height);
        top = Mathf.Min(top, area.yMax - ScreenMargin);
        rect.localPosition = new Vector3(left + width * 0.5f, top - height * 0.5f, 0f);
    }
}
