using System;
using UnityEngine;

/// <summary>
/// カーソルから敵までの電撃。表示が終わったらプールへ戻す。
/// </summary>
public class LightningBolt : MonoBehaviour
{
    const int SegmentCount = 4;

    [SerializeField] Sprite sprite;
    [SerializeField] float width = 0.08f;
    [SerializeField] Color color = new Color(0.62f, 0.9f, 1f, 1f);

    SpriteRenderer[] segments;
    float[] jagged = new float[SegmentCount + 1];
    Action<LightningBolt> release;
    float duration;
    float remaining;
    bool playing;

    public void Play(Vector3 from, Vector3 to, float duration, Action<LightningBolt> release)
    {
        this.release = release;
        this.duration = Mathf.Max(0.05f, duration);
        remaining = this.duration;
        playing = true;

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        EnsureSegments();
        ApplySprite();
        RandomizeJagged();
        Place(from, to);
        ApplyAlpha(1f);
    }

    void Update()
    {
        if (!playing) return;

        remaining -= Time.deltaTime;
        ApplyAlpha(Mathf.Clamp01(remaining / duration));
        if (remaining > 0f) return;
        Finish();
    }

    void Finish()
    {
        if (!playing) return;
        playing = false;
        release?.Invoke(this);
    }

    void EnsureSegments()
    {
        if (segments != null) return;

        segments = new SpriteRenderer[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            var child = new GameObject("Bolt");
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 40;
            segments[i] = renderer;
        }
    }

    void RandomizeJagged()
    {
        jagged[0] = 0f;
        jagged[SegmentCount] = 0f;
        for (int i = 1; i < SegmentCount; i++)
            jagged[i] = UnityEngine.Random.Range(-0.28f, 0.28f);
    }

    void Place(Vector3 from, Vector3 to)
    {
        from.z = 0f;
        to.z = 0f;
        Vector3 delta = to - from;
        Vector3 normal = new Vector3(-delta.y, delta.x, 0f);
        if (normal.sqrMagnitude > 0.0001f)
            normal.Normalize();

        for (int i = 0; i < SegmentCount; i++)
        {
            float t0 = i / (float)SegmentCount;
            float t1 = (i + 1) / (float)SegmentCount;
            Vector3 start = Vector3.Lerp(from, to, t0) + normal * jagged[i];
            Vector3 end = Vector3.Lerp(from, to, t1) + normal * jagged[i + 1];
            var segment = segments[i].transform;
            segment.position = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end);
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            segment.rotation = Quaternion.Euler(0f, 0f, angle);
            segment.localScale = AttackVisual.ScaleTo(segments[i].sprite, new Vector2(Mathf.Max(0.05f, length), width));
        }
    }

    void ApplySprite()
    {
        var visual = AttackVisual.Resolve(sprite, false);
        for (int i = 0; i < segments.Length; i++)
            segments[i].sprite = visual;
    }

    void ApplyAlpha(float alpha)
    {
        var tint = color;
        tint.a *= alpha;
        for (int i = 0; i < segments.Length; i++)
            segments[i].color = tint;
    }
}
