using System.Collections.Generic;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワクチンタブの、研究中のフラスコのアイコン。
/// - 液の高さ：ワクチンの完成度（VaccineRuntime.Progress）。注ぐと、ゆっくり上がる。
/// - 泡：液があるあいだ、底から水面まで昇る。完成度が上がるほど多い。
/// - ふだん：ゆらゆら傾く。
/// - 注いだとき：ぽよんと弾み、泡がまとめて出る。工程が終わったときは、液が白く光る。
/// - 完成したとき（満タンで、まだ撒いていない）：緑の液が明るくなったり暗くなったりし、フラスコの後ろがふわっと光る。
/// - 撒いたあと：フラスコは空っぽ。内側の数か所に、光るしずくが付いて残る（dropPixels の位置）。
/// 絵は Assets/Texture/UI/Vaccine の 5 枚（フラスコの枠・液・泡・しずく・光）。どれも白で、Image の色で染める。
/// 泡としずくは、コードで作って並べる。
/// </summary>
public class UI_VaccineFlask : MonoBehaviour
{
    [Tooltip("揺らす本体（枠・液・泡をまとめた親）")]
    [SerializeField] RectTransform body;
    [Tooltip("液。Image Type は Filled（Vertical・Bottom）")]
    [SerializeField] Image image_liquid;
    [Tooltip("泡を出す範囲。液と同じ大きさにする")]
    [SerializeField] RectTransform bubbleArea;
    [SerializeField] Sprite bubbleSprite;
    [Tooltip("完成したときにフラスコの後ろで光る板")]
    [SerializeField] Image image_glow;
    [SerializeField] Sprite dropSprite;
    [Tooltip("しずくのまわりの光")]
    [SerializeField] Sprite glowSprite;
    [Tooltip("撒いたあとに残るしずくの位置。フラスコの絵のドット（左上が 0,0。横 24・縦 32）")]
    [SerializeField] Vector2[] dropPixels =
    {
        new Vector2(6, 25), new Vector2(17, 22), new Vector2(10, 28), new Vector2(14, 17), new Vector2(11, 7),
    };

    [Header("色")]
    [SerializeField] Color liquidColor = new Color(0.35f, 0.95f, 0.55f, 0.9f);
    [SerializeField] Color flashColor = new Color(0.9f, 1f, 0.92f, 1f);
    [SerializeField] Color bubbleColor = new Color(0.85f, 1f, 0.9f, 0.85f);
    [Tooltip("完成したときの、フラスコの後ろの光")]
    [SerializeField] Color glowColor = new Color(0.45f, 1f, 0.6f, 0.6f);
    [SerializeField] Color dropColor = new Color(0.6f, 1f, 0.72f, 1f);
    [SerializeField] Color dropGlowColor = new Color(0.45f, 1f, 0.6f, 0.55f);

    [Header("動き")]
    [Tooltip("ふだん傾く角度（度）")]
    [SerializeField] float swayAngle = 3f;
    [Tooltip("1 秒に出る泡の数。完成度 0 → 1 でこの間を動く")]
    [SerializeField] Vector2 bubbleRate = new Vector2(1.5f, 6f);
    [Tooltip("泡が昇る速さ（UI の単位/秒）")]
    [SerializeField] Vector2 bubbleSpeed = new Vector2(40f, 90f);
    [Tooltip("泡の大きさ（UI の単位）")]
    [SerializeField] float bubbleSize = 24f;

    // 絵の形（ui_vaccine_liquid は 24×20 ドット）。泡が胴の外に出ないように、高さごとの幅をここで持つ。
    const int LiquidPixelsW = 24;
    const int LiquidPixelsH = 20;
    const int MaxBubbles = 24;
    // 液の高さが追いつく速さ（1 秒あたりの割合）
    const float FillSpeed = 0.5f;
    // フラスコの絵のドット 1 つの大きさ（UI の単位）と、絵の縦のドット数。
    const float PixelSize = 8f;
    const int FlaskPixelsH = 32;
    const float DropGlowSize = 72f;

    class Bubble
    {
        public Image image;
        public float y;
        public float speed;
        public float phase;
        public float scale;
        public bool alive;
    }

    readonly List<Bubble> bubbles = new();
    readonly List<Image> drops = new();
    readonly List<Image> dropGlows = new();
    float glowLevel;
    float shownFill;
    float spawnWait;
    int lastPours = -1;
    int lastStages = -1;
    Tween flashTween;
    Tween punchTween;
    bool subscribed;

    void OnEnable()
    {
        if (!subscribed)
        {
            subscribed = true;
            VaccineRuntime.Changed.Subscribe(_ => OnChanged()).AddTo(this);
        }
        // タブを開いたときは、今の高さから始める（伸びる動きは注いだときだけ）。
        shownFill = TargetFill();
        glowLevel = 0f;
        lastPours = VaccineRuntime.Pours;
        lastStages = VaccineRuntime.StagesDone;
        if (image_liquid != null)
        {
            image_liquid.color = liquidColor;
            image_liquid.fillAmount = shownFill;
        }
        foreach (var bubble in bubbles) Hide(bubble);
        EnsureDrops();
    }

    /// <summary>撒いたあとは空っぽ。それまでは完成度。</summary>
    static float TargetFill() => VaccineRuntime.Cleared ? 0f : VaccineRuntime.Progress;

    void OnDisable()
    {
        flashTween?.Kill();
        punchTween?.Kill();
        if (body != null) body.localScale = Vector3.one;
    }

    void OnChanged()
    {
        if (!isActiveAndEnabled) return;
        int pours = VaccineRuntime.Pours;
        int stages = VaccineRuntime.StagesDone;
        if (lastPours >= 0 && pours > lastPours)
        {
            Punch();
            Burst(6);
            if (stages > lastStages) Flash();
        }
        lastPours = pours;
        lastStages = stages;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        float target = TargetFill();
        bool complete = VaccineRuntime.IsFull && !VaccineRuntime.Cleared;
        bool cleared = VaccineRuntime.Cleared;
        shownFill = Mathf.MoveTowards(shownFill, target, FillSpeed * dt);

        if (body != null)
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * swayAngle);

        if (image_liquid != null)
        {
            // 水面が少し波打つ。空と満タンのときは揺らさない。
            float wave = shownFill > 0.001f && shownFill < 0.999f ? Mathf.Sin(t * 3.1f) * 0.006f : 0f;
            image_liquid.fillAmount = Mathf.Clamp01(shownFill + wave);
            // 完成したら、緑のまま、ゆっくり明るくなったり暗くなったりする。
            if (complete && (flashTween == null || !flashTween.IsActive()))
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f);
                image_liquid.color = Color.Lerp(liquidColor, flashColor, 0.15f + 0.3f * pulse);
            }
        }

        // フラスコの後ろの光。完成したときだけ、ふわっと出して脈打たせる。
        glowLevel = Mathf.MoveTowards(glowLevel, complete ? 1f : 0f, dt * 1.5f);
        if (image_glow != null)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 2f);
            var c = glowColor;
            c.a *= glowLevel * pulse;
            image_glow.color = c;
            image_glow.enabled = c.a > 0.001f;
            image_glow.rectTransform.localScale = Vector3.one * (0.95f + 0.08f * pulse);
        }

        UpdateDrops(cleared, t);
        UpdateBubbles(dt, t);
    }

    #region -- 泡 --
    void UpdateBubbles(float dt, float t)
    {
        if (bubbleArea == null || bubbleSprite == null) return;
        float height = bubbleArea.rect.height;
        float surface = shownFill * height;

        if (shownFill > 0.02f)
        {
            spawnWait -= dt;
            if (spawnWait <= 0f)
            {
                float rate = Mathf.Lerp(bubbleRate.x, bubbleRate.y, shownFill);
                spawnWait = 1f / Mathf.Max(0.1f, rate) * Random.Range(0.6f, 1.4f);
                Spawn();
            }
        }

        foreach (var bubble in bubbles)
        {
            if (!bubble.alive) continue;
            bubble.y += bubble.speed * dt;
            if (bubble.y >= surface)
            {
                Hide(bubble);
                continue;
            }
            float half = HalfWidthAt(bubble.y / Mathf.Max(1f, height)) * bubbleArea.rect.width - bubbleSize * 0.5f * bubble.scale;
            float x = Mathf.Sin(t * 2.2f + bubble.phase) * Mathf.Max(0f, half) * 0.45f + Mathf.Cos(bubble.phase) * Mathf.Max(0f, half) * 0.4f;
            var rt = (RectTransform)bubble.image.transform;
            rt.anchoredPosition = new Vector2(x, bubble.y);
            // 水面に近づくと消えていく。
            float fade = Mathf.Clamp01((surface - bubble.y) / (bubbleSize * 1.5f));
            var c = bubbleColor;
            c.a *= fade;
            bubble.image.color = c;
        }
    }

    void Burst(int count)
    {
        for (int i = 0; i < count; i++) Spawn();
    }

    void Spawn()
    {
        if (bubbleArea == null || bubbleSprite == null) return;
        var bubble = Take();
        if (bubble == null) return;
        bubble.alive = true;
        bubble.y = Random.Range(0f, bubbleSize);
        bubble.speed = Random.Range(bubbleSpeed.x, bubbleSpeed.y);
        bubble.phase = Random.Range(0f, Mathf.PI * 2f);
        bubble.scale = Random.Range(0.6f, 1f);
        bubble.image.rectTransform.sizeDelta = Vector2.one * bubbleSize * bubble.scale;
        bubble.image.enabled = true;
    }

    Bubble Take()
    {
        foreach (var bubble in bubbles)
            if (!bubble.alive) return bubble;
        if (bubbles.Count >= MaxBubbles) return null;

        var go = new GameObject("bubble", typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(bubbleArea, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.sprite = bubbleSprite;
        image.raycastTarget = false;
        var created = new Bubble { image = image };
        bubbles.Add(created);
        return created;
    }

    static void Hide(Bubble bubble)
    {
        bubble.alive = false;
        if (bubble.image != null) bubble.image.enabled = false;
    }

    /// <summary>液の絵の、高さ（0＝底、1＝首の付け根）ごとの内側の半分の幅（液の幅に対する割合）。絵を作った式と同じ。</summary>
    static float HalfWidthAt(float height01)
    {
        // 液の絵の行（上から）。0 行目が首の付け根（フラスコの 11 行目）。
        int row = Mathf.Clamp(LiquidPixelsH - 1 - Mathf.FloorToInt(height01 * LiquidPixelsH), 0, LiquidPixelsH - 1);
        int flaskRow = row + 11;
        float half;
        if (flaskRow <= 29) half = Mathf.Min(11f, 3f + (flaskRow - 10) * 0.5f);
        else half = 10f;
        // 枠の 1 ドットを引く。
        return Mathf.Max(0f, half - 1.5f) / LiquidPixelsW;
    }
    #endregion

    #region -- しずく（撒いたあと） --
    void EnsureDrops()
    {
        if (body == null || dropSprite == null || dropPixels == null || drops.Count > 0) return;
        // 液と枠の間に置く（枠の線がしずくの上にくるように）。
        int index = image_liquid != null ? image_liquid.transform.GetSiblingIndex() + 1 : body.childCount;
        for (int i = 0; i < dropPixels.Length; i++)
        {
            var p = dropPixels[i];
            var center = new Vector2((p.x + 0.5f) * PixelSize, (FlaskPixelsH - p.y - 0.5f) * PixelSize);
            if (glowSprite != null)
                dropGlows.Add(MakeImage("dropGlow", glowSprite, center, Vector2.one * DropGlowSize, index++));
            var size = new Vector2(dropSprite.rect.width, dropSprite.rect.height) * PixelSize;
            drops.Add(MakeImage("drop", dropSprite, center, size, index++));
        }
    }

    Image MakeImage(string name, Sprite sprite, Vector2 position, Vector2 size, int siblingIndex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(body, false);
        rt.SetSiblingIndex(Mathf.Min(siblingIndex, body.childCount - 1));
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    void UpdateDrops(bool cleared, float t)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            // しずくごとに、ずらして瞬かせる。
            float twinkle = 0.5f + 0.5f * Mathf.Sin(t * (1.4f + i * 0.23f) + i * 1.7f);
            var drop = drops[i];
            drop.enabled = cleared;
            if (cleared) drop.color = Color.Lerp(dropColor, Color.white, twinkle * 0.4f);
            if (i < dropGlows.Count)
            {
                var glow = dropGlows[i];
                glow.enabled = cleared;
                if (cleared)
                {
                    var c = dropGlowColor;
                    c.a *= 0.4f + 0.6f * twinkle;
                    glow.color = c;
                }
            }
        }
    }
    #endregion

    #region -- 弾む・光る --
    void Punch()
    {
        if (body == null) return;
        punchTween?.Kill();
        body.localScale = Vector3.one;
        punchTween = body.DOPunchScale(new Vector3(0.08f, -0.06f, 0f), 0.35f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
    }

    void Flash()
    {
        if (image_liquid == null) return;
        flashTween?.Kill();
        image_liquid.color = flashColor;
        flashTween = image_liquid.DOColor(liquidColor, 0.8f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }
    #endregion
}
