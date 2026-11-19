using UnityEngine;

/// <summary>壁の帯の種類。</summary>
public enum WallBandKind
{
    /// <summary>加速帯。触れたボールが速くなる。</summary>
    Boost,
    /// <summary>減速帯。触れたボールが遅くなる。</summary>
    Slow,
}

/// <summary>
/// 壁の仕掛け（加速帯・減速帯）。左右の壁の一部に、決まった時間だけ現れる。
/// 触れたボールは、しばらく速さが変わる（BlockBall.ChangeSpeed）。
/// 当たり判定は壁の内側の面に沿ったトリガーで、ボールの跳ね返りは壁がそのまま受け持つ。
///
/// 見た目はプレハブ（SO_BlockSession の wallBoostBand・wallSlowBand）の子に置く。
/// - プレハブは「左の壁」に出たときの形で作る。原点が壁の内側の面で、+x がフィールド側。右の壁では body を左右反転する。
/// - プレハブの高さは designHeight。実際の帯の高さに合わせて、板と光は縦に伸ばし、矢印は大きさを変えずに y の位置だけ広げる。
/// - 色はこのコンポーネントの「色」で決める（子の SpriteRenderer の色は、インスペクターで変えたときに合わせて塗り直す）。
/// </summary>
public class BlockWallBand : MonoBehaviour
{
    const float AppearTime = 0.3f;
    const float BlinkTime = 1.5f;
    const float FadeTime = 0.4f;
    const float FlashTime = 0.15f;
    // トリガーを壁の中へ入れる深さ。ボールが壁に沈みかけても拾えるようにする。
    const float TriggerDepth = 0.1f;

    [Header("見た目（子）")]
    [Tooltip("板・光・矢印の親。出るときに縦へ伸ばし、右の壁では左右反転する。")]
    [SerializeField] Transform body;
    [Tooltip("壁に沿った板。")]
    [SerializeField] SpriteRenderer plate;
    [Tooltip("板の外側から壁の中へ広がる光。")]
    [SerializeField] SpriteRenderer glow;
    [Tooltip("矢印。光る順番は y の位置で決める（加速は下から、減速は上から）。")]
    [SerializeField] SpriteRenderer[] arrows;
    [Tooltip("プレハブを作ったときの帯の高さ（ワールド単位）。実際の高さとの比で、板・光の長さと矢印の間隔を変える。")]
    [SerializeField] float designHeight = 0.7f;

    [Header("色")]
    [Tooltip("板の色。プレイ中に変えるとすぐ反映される。")]
    [SerializeField] Color plateColor = new Color(1f, 0.84f, 0.1f, 1f);
    [Tooltip("矢印の色。光っていないときは、この色の 35% の濃さになる。")]
    [SerializeField] Color arrowColor = new Color(1f, 1f, 0.78f, 1f);
    [Tooltip("光の色。a で濃さを決める。")]
    [SerializeField] Color glowColor = new Color(1f, 0.88f, 0.2f, 0.32f);
    [Tooltip("帯に触れたボールが寄る色。効果が切れるにつれて元の色へ戻る。")]
    [SerializeField] Color ballTint = new Color(1f, 0.85f, 0.15f, 1f);

    WallBandKind kind;

    BlockSession session;
    float life;
    float age;
    float rate;
    float boostTime;
    float flash;
    float height;
    int inward;
    // 光る順番に並べた矢印（加速は下から、減速は上から）。
    SpriteRenderer[] waveOrder;

    public bool Expired { get; private set; }
    public float CenterY => transform.position.y;
    public float Height => height;
    public int Side => -inward;
    public WallBandKind Kind => kind;
    public Color BallTint => ballTint;

    /// <summary>
    /// faceX は壁の内側の面の x。inward は壁から見てフィールドの向き（左の壁なら 1、右の壁なら -1）。
    /// </summary>
    public void Init(BlockSession owner, WallBandKind bandKind, float faceX, float centerY, float spanHeight, int inwardSign,
        float reach, float lifeTime, float boostRate, float boostSeconds)
    {
        session = owner;
        kind = bandKind;
        inward = inwardSign >= 0 ? 1 : -1;
        height = Mathf.Max(0.1f, spanHeight);
        life = Mathf.Max(AppearTime + FadeTime, lifeTime);
        rate = boostRate;
        boostTime = boostSeconds;
        transform.position = new Vector3(faceX, centerY, 0f);

        reach = Mathf.Max(0.02f, reach);
        var trigger = GetComponent<BoxCollider2D>();
        if (trigger == null)
            trigger = gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(TriggerDepth + reach, height);
        trigger.offset = new Vector2(inward * (reach - TriggerDepth) * 0.5f, 0f);

        if (body == null)
        {
            Debug.LogWarning($"{name}：body が設定されていません。プレハブの子を確認してください。");
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
        }
        FitHeight(height / Mathf.Max(0.01f, designHeight));
        SortArrows();
        body.localScale = new Vector3(inward, 0f, 1f);
        ApplyColors(1f, 0f);
    }

    /// <summary>板と光は縦に伸ばし、矢印は y の位置だけ広げる。</summary>
    void FitHeight(float ratio)
    {
        Stretch(plate, ratio, true);
        Stretch(glow, ratio, true);
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++)
            Stretch(arrows[i], ratio, false);
    }

    static void Stretch(SpriteRenderer sprite, float ratio, bool length)
    {
        if (sprite == null) return;
        var t = sprite.transform;
        var position = t.localPosition;
        position.y *= ratio;
        t.localPosition = position;
        if (!length) return;
        var scale = t.localScale;
        scale.y *= ratio;
        t.localScale = scale;
    }

    void SortArrows()
    {
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        if (arrows != null)
        {
            for (int i = 0; i < arrows.Length; i++)
                if (arrows[i] != null) list.Add(arrows[i]);
        }
        bool slow = kind == WallBandKind.Slow;
        list.Sort((a, b) =>
        {
            int c = a.transform.localPosition.y.CompareTo(b.transform.localPosition.y);
            return slow ? -c : c;
        });
        waveOrder = list.ToArray();
    }

    void Update()
    {
        if (Expired) return;
        age += Time.deltaTime;
        flash = Mathf.Max(0f, flash - Time.deltaTime);

        if (age >= life)
        {
            Expired = true;
            session?.NotifyWallBandGone(this);
            Destroy(gameObject);
            return;
        }

        // 出る：縦に伸びる。
        float grow = Mathf.Clamp01(age / AppearTime);
        body.localScale = new Vector3(inward, 1f - (1f - grow) * (1f - grow), 1f);

        // 消える前は点滅し、最後はフェードアウトする。
        float alpha = 1f;
        float left = life - age;
        if (left < FadeTime)
            alpha = left / FadeTime;
        else if (left < BlinkTime + FadeTime)
            alpha = Mathf.Repeat(age * 6f, 1f) < 0.5f ? 1f : 0.35f;

        ApplyColors(alpha, flash / FlashTime);
    }

    void ApplyColors(float alpha, float hit)
    {
        if (plate != null)
            plate.color = Fade(Color.Lerp(plateColor, Color.white, hit * 0.8f), alpha);
        if (glow != null)
            glow.color = Fade(glowColor, alpha * (1f + hit * 1.5f));
        if (waveOrder == null) return;

        // 加速帯の矢印は下から順に光って上へ流れ、減速帯は上から順にゆっくり沈む。
        bool slow = kind == WallBandKind.Slow;
        int count = waveOrder.Length;
        for (int i = 0; i < count; i++)
        {
            float wave = Mathf.Repeat(age * (slow ? 1.1f : 2.2f) - i / (float)count, 1f);
            float light = wave < 0.35f ? 1f : 0.35f;
            waveOrder[i].color = Fade(Color.Lerp(arrowColor, Color.white, hit), alpha * Mathf.Max(light, hit));
        }
    }

    static Color Fade(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(color.a * alpha);
        return color;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Expired || age < AppearTime * 0.5f || other == null) return;
        var ball = other.GetComponentInParent<BlockBall>();
        if (ball == null) return;
        ball.ChangeSpeed(rate, boostTime, ballTint);
        flash = FlashTime;
    }

#if UNITY_EDITOR
    /// <summary>プレハブを開いているとき、色の項目を子の SpriteRenderer に映す。</summary>
    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (plate != null) plate.color = plateColor;
        if (glow != null) glow.color = glowColor;
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++)
            if (arrows[i] != null) arrows[i].color = arrowColor;
    }
#endif
}
