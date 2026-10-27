using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カーソルを中心に置き、線の幅が太くなるレーザー。接触した敵は 1 本につき 1 回ダメージを受ける。
/// </summary>
public class LaserBeam : MonoBehaviour
{
    readonly List<IDamagable> overlap = new();
    readonly HashSet<IDamagable> hit = new();

    [SerializeField] Sprite sprite;
    [SerializeField] Color color = new Color(1f, 0.32f, 0.24f, 0.92f);
    [SerializeField] float length = 32f;
    [SerializeField] float minWidth = 0.05f;
    [SerializeField] float maxWidth = 1.15f;
    [SerializeField] float growDuration = 0.18f;
    [SerializeField] float holdDuration = 0.08f;

    Action<LaserBeam> release;
    Vector3 origin;
    float width;
    int damage;
    int attackIndex = -1;
    float elapsed;
    float baseHold = -1f;
    bool playing;

    public void Play(Vector3 origin, float angle, int damage, float width, float grow, Action<LaserBeam> release, int attackIndex = -1, float extraHold = 0f)
    {
        this.release = release;
        this.origin = origin;
        this.damage = damage;
        this.attackIndex = attackIndex;
        if (baseHold < 0f) baseHold = holdDuration;
        holdDuration = baseHold + Mathf.Max(0f, extraHold);
        if (width > 0f) maxWidth = width;
        if (grow > 0f) growDuration = grow;
        elapsed = 0f;
        playing = true;
        hit.Clear();

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        gameObject.SetActive(true);
        EnsureVisual();
        ApplyWidth(minWidth);
        DamageOverlaps();
    }

    void Update()
    {
        if (!playing) return;

        elapsed += Time.deltaTime;
        float grow = Mathf.Max(0.01f, growDuration);
        float t = Mathf.Clamp01(elapsed / grow);
        float eased = 1f - (1f - t) * (1f - t);
        ApplyWidth(Mathf.Lerp(minWidth, maxWidth, eased));
        DamageOverlaps();

        if (elapsed >= grow + holdDuration)
            Finish();
    }

    void ApplyWidth(float nextWidth)
    {
        width = nextWidth;
        float beamLength = Mathf.Max(0.1f, length);
        origin.z = 0f;
        var visual = sprite != null ? sprite : AttackVisual.White;
        var scale = AttackVisual.ScaleTo(visual, new Vector2(beamLength, width));
        transform.localScale = scale;

        var center = visual != null ? visual.bounds.center : Vector3.zero;
        center.x *= scale.x;
        center.y *= scale.y;
        transform.position = origin - transform.rotation * center;
    }

    void DamageOverlaps()
    {
        AttackHitQuery.Box(origin, new Vector2(length, width), transform.eulerAngles.z, overlap);
        for (int i = 0; i < overlap.Count; i++)
        {
            var target = overlap[i];
            if (!hit.Add(target)) continue;
            if (!target.isAlive) continue;
            target.Damage(damage, attackIndex: attackIndex, enemiesInArea: overlap.Count);
        }
    }

    void Finish()
    {
        if (!playing) return;
        playing = false;
        hit.Clear();
        release?.Invoke(this);
    }

    void EnsureVisual()
    {
        if (!TryGetComponent(out SpriteRenderer renderer))
            renderer = gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = AttackVisual.Resolve(sprite, false);
        renderer.color = color;
        renderer.sortingOrder = 28;
        renderer.flipX = false;
        renderer.flipY = false;
    }
}
