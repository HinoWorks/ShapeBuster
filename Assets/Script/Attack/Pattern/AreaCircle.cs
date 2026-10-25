using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一定時間その場に残り、間隔ごとに菱形範囲内の敵へダメージを与える。
/// 菱形は正方形を45°回転した形で、半径は中心から上下左右の頂点まで。
/// </summary>
public class AreaCircle : MonoBehaviour
{
    readonly List<IDamagable> overlap = new();

    [SerializeField] Sprite sprite;
    [SerializeField] Color color = new Color(1f, 0.42f, 0.15f, 0.42f);

    Action<AreaCircle> release;
    SpriteRenderer fill;
    float radius;
    float duration;
    float damageInterval;
    float elapsed;
    float nextTick;
    int damage;
    int attackIndex = -1;
    bool playing;

    public void Play(Vector3 position, float radius, float duration, float damageInterval, int damage, Action<AreaCircle> release, int attackIndex = -1)
    {
        this.release = release;
        this.attackIndex = attackIndex;
        this.radius = Mathf.Max(0.1f, radius);
        this.duration = Mathf.Max(0.1f, duration);
        this.damageInterval = Mathf.Max(0.05f, damageInterval);
        this.damage = damage;
        elapsed = 0f;
        nextTick = 0f;
        playing = true;

        position.z = 0f;
        transform.position = position;
        transform.rotation = Quaternion.identity;
        gameObject.SetActive(true);
        EnsureVisual();
        TickWhileDue();
    }

    void Update()
    {
        if (!playing) return;

        elapsed += Time.deltaTime;
        TickWhileDue();
        UpdateVisual();
        if (elapsed >= duration)
            Finish();
    }

    void TickWhileDue()
    {
        while (playing && nextTick < duration && elapsed >= nextTick)
        {
            AttackHitQuery.Diamond(transform.position, radius, overlap);
            AttackHitQuery.DamageAll(overlap, damage, attackIndex);
            nextTick += damageInterval;
        }
    }

    void UpdateVisual()
    {
        float appear = Mathf.Clamp01(elapsed / 0.12f);
        float fade = elapsed > duration - 0.25f
            ? Mathf.Clamp01((duration - elapsed) / 0.25f)
            : 1f;
        float pulse = 1f + Mathf.Sin(elapsed * 8f) * 0.03f;
        float diameter = radius * 2f * appear * pulse;
        transform.localScale = AttackVisual.ScaleTo(fill != null ? fill.sprite : sprite, new Vector2(diameter, diameter));

        if (fill == null) return;
        var tint = color;
        tint.a *= fade;
        fill.color = tint;
    }

    void Finish()
    {
        if (!playing) return;
        playing = false;
        release?.Invoke(this);
    }

    void EnsureVisual()
    {
        if (fill == null)
        {
            fill = GetComponent<SpriteRenderer>();
            if (fill == null)
                fill = gameObject.AddComponent<SpriteRenderer>();
            fill.sprite = sprite != null ? sprite : AttackVisual.Diamond;
            fill.sortingOrder = 12;
        }
        UpdateVisual();
    }
}
