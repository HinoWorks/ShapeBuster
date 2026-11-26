using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カーソル方向へ、反射を含んだ点線と先端を出す。太さや見た目はプレハブで調整する。
/// </summary>
public class BlockAimGuide : MonoBehaviour
{
    [SerializeField] Transform pullBar;
    [SerializeField] Transform shotBar;
    [SerializeField] Transform shotTip;
    [SerializeField] float maxLength = 36f;
    [SerializeField] float dotLength = 0.08f;
    [SerializeField] float dotGap = 0.1f;

    [Header("発射地点のマーク")]
    [Tooltip("F で発射地点を決めたときに出すマーク（SpriteLaunchMark のマテリアル）。無ければ出さない")]
    [SerializeField] SpriteRenderer launchMark;
    [Tooltip("輪の半径（ワールドの長さ）")]
    [SerializeField] float markRadius = 0.21f;
    [Tooltip("押したとき、輪がこの倍率から縮んで止まる")]
    [SerializeField] float lockStartScale = 2.4f;
    [SerializeField] float lockTime = 0.14f;
    [Tooltip("外側の弧がまわる速さ（ラジアン／秒）")]
    [SerializeField] float spinSpeed = 1.6f;
    [Tooltip("輪が脈打つ速さ（ラジアン／秒）")]
    [SerializeField] float pulseSpeed = 6f;
    [Tooltip("撃ったときの波紋の時間（秒）")]
    [SerializeField] float burstTime = 0.32f;
    [Tooltip("取りやめたときにしぼむ時間（秒）")]
    [SerializeField] float cancelTime = 0.12f;

    readonly List<Transform> dots = new();
    float shotThickness = 0.07f;
    Vector3 tipScale = Vector3.one * 0.18f;

    enum MarkState { Off, Lock, Hold, Burst, Cancel }
    MarkState markState;
    float markTimer;
    float markSpin;
    MaterialPropertyBlock markBlock;
    static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    static readonly int RingScaleId = Shader.PropertyToID("_RingScale");
    static readonly int SpinId = Shader.PropertyToID("_Spin");
    static readonly int PulseId = Shader.PropertyToID("_Pulse");
    static readonly int BurstId = Shader.PropertyToID("_Burst");
    static readonly int FadeId = Shader.PropertyToID("_Fade");

    public float PredictionLength => Mathf.Max(4f, maxLength);

    void Awake()
    {
        if (shotBar != null)
            shotThickness = Mathf.Abs(shotBar.localScale.y);
        if (shotTip != null)
            tipScale = shotTip.localScale;
        EnsureSprite(shotBar, false);
        EnsureSprite(shotTip, true);
        EnsureSprite(launchMark != null ? launchMark.transform : null, false);
        Hide();
        SetMark(MarkState.Off);
    }

    // ---- 発射地点のマーク ----

    /// <summary>発射地点を決めた。輪が大きいところから縮んで止まる。</summary>
    public void PinLaunch(Vector2 position)
    {
        if (launchMark == null) return;
        var t = launchMark.transform;
        t.position = new Vector3(position.x, position.y, t.position.z);
        // 波紋（3 倍）とまわる弧まで入る大きさにする。
        float size = Mathf.Max(0.05f, markRadius) * 2f * 3.5f;
        t.rotation = Quaternion.identity;
        t.localScale = new Vector3(size, size, 1f);
        SetMark(MarkState.Lock);
    }

    /// <summary>撃った。輪が波紋になって広がり、消える。</summary>
    public void FireLaunch()
    {
        if (markState == MarkState.Off) return;
        SetMark(MarkState.Burst);
    }

    /// <summary>撃たずに離した。輪が小さくしぼんで消える。</summary>
    public void CancelLaunch()
    {
        if (markState == MarkState.Off) return;
        SetMark(MarkState.Cancel);
    }

    void SetMark(MarkState state)
    {
        markState = state;
        markTimer = 0f;
        if (launchMark != null)
            launchMark.gameObject.SetActive(state != MarkState.Off);
        if (state != MarkState.Off)
            ApplyMark();
    }

    void Update()
    {
        if (launchMark == null || markState == MarkState.Off) return;
        float dt = Time.deltaTime;
        markTimer += dt;
        markSpin = Mathf.Repeat(markSpin + spinSpeed * dt, Mathf.PI * 2f);

        if (markState == MarkState.Lock && markTimer >= lockTime)
            SetMark(MarkState.Hold);
        else if (markState == MarkState.Burst && markTimer >= burstTime)
            SetMark(MarkState.Off);
        else if (markState == MarkState.Cancel && markTimer >= cancelTime)
            SetMark(MarkState.Off);
        else
            ApplyMark();
    }

    void ApplyMark()
    {
        float scale = 1f, burst = 0f, fade = 1f;
        switch (markState)
        {
            case MarkState.Lock:
            {
                float k = Mathf.Clamp01(markTimer / Mathf.Max(0.01f, lockTime));
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                scale = Mathf.Lerp(lockStartScale, 1f, ease);
                fade = k;
                break;
            }
            case MarkState.Burst:
                burst = Mathf.Clamp01(markTimer / Mathf.Max(0.01f, burstTime));
                break;
            case MarkState.Cancel:
            {
                float k = Mathf.Clamp01(markTimer / Mathf.Max(0.01f, cancelTime));
                scale = Mathf.Lerp(1f, 0.4f, k);
                fade = 1f - k;
                break;
            }
        }
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);

        markBlock ??= new MaterialPropertyBlock();
        launchMark.GetPropertyBlock(markBlock);
        markBlock.SetFloat(RingRadiusId, markRadius);
        markBlock.SetFloat(RingScaleId, scale);
        markBlock.SetFloat(SpinId, markSpin);
        markBlock.SetFloat(PulseId, pulse);
        markBlock.SetFloat(BurstId, burst);
        markBlock.SetFloat(FadeId, fade);
        launchMark.SetPropertyBlock(markBlock);
    }

    public void Show(Vector2 origin, Vector2 pull, List<Vector2> path)
    {
        if (pull.sqrMagnitude < 0.0025f)
        {
            Hide();
            return;
        }

        SetActive(pullBar, false);
        int segments = path == null ? 0 : path.Count - 1;
        int used = 0;
        for (int i = 0; i < segments; i++)
            used = ShowDots(path[i], path[i + 1], used);

        if (used == 0)
            SetActive(shotBar, false);
        for (int i = used; i < dots.Count; i++)
            SetActive(dots[i], false);

        if (shotTip == null || segments <= 0)
        {
            SetActive(shotTip, false);
            return;
        }

        var end = path[path.Count - 1];
        shotTip.gameObject.SetActive(true);
        shotTip.position = new Vector3(end.x, end.y, 0f);
        shotTip.localScale = tipScale;
    }

    public void Hide()
    {
        SetActive(pullBar, false);
        SetActive(shotBar, false);
        SetActive(shotTip, false);
        for (int i = 0; i < dots.Count; i++)
            SetActive(dots[i], false);
    }

    int ShowDots(Vector2 from, Vector2 to, int used)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.02f) return used;

        float mark = Mathf.Max(0.02f, dotLength);
        float gap = Mathf.Max(0.02f, dotGap);
        float step = mark + gap;
        Vector2 dir = delta / length;
        float cursor = 0f;
        while (cursor < length - 0.01f)
        {
            float dash = Mathf.Min(mark, length - cursor);
            if (dash < 0.02f) break;
            ShowBar(Dot(used), from + dir * cursor, from + dir * (cursor + dash), shotThickness);
            used++;
            cursor += step;
        }
        return used;
    }

    Transform Dot(int index)
    {
        if (shotBar == null) return null;
        if (dots.Count == 0)
            dots.Add(shotBar);

        while (dots.Count <= index)
        {
            var copy = Instantiate(shotBar, shotBar.parent);
            copy.gameObject.SetActive(false);
            dots.Add(copy);
        }
        return dots[index];
    }

    static void EnsureSprite(Transform target, bool circle)
    {
        if (target == null) return;
        var sprite = target.GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite != null) return;
        sprite.sprite = circle ? BlockSession.CircleSprite : BlockSession.SquareSprite;
    }

    static void ShowBar(Transform bar, Vector2 from, Vector2 to, float thickness)
    {
        if (bar == null) return;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.02f)
        {
            bar.gameObject.SetActive(false);
            return;
        }

        bar.gameObject.SetActive(true);
        bar.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0f);
        bar.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        bar.localScale = new Vector3(length, thickness, 1f);
    }

    static void SetActive(Transform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }
}
