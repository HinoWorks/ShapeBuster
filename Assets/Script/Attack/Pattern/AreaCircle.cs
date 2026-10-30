using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一定時間その場に残り、間隔ごとに子の TriggerSender へ重なる敵へダメージを与える。
/// 当たりの形は子の Collider2D。ルートのスケールだけ半径に合わせ、絵の出現はスプライト側。
/// </summary>
public class AreaCircle : MonoBehaviour
{
    readonly List<IDamagable> overlap = new();

    [SerializeField] Color color = new Color(1f, 0.42f, 0.15f, 0.42f);
    [SerializeField] Material fillMaterial;
    [SerializeField] TriggerSender hitSender;

    Action<AreaCircle> release;
    SpriteRenderer fill;
    Vector3 visualBaseScale = Vector3.one;
    bool visualScaleCaptured;
    Collider2D hitCollider;
    bool warnedMissingHit;
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
        if (hitCollider == null) return;

        // 出した直後と、スケールを変えた直後は物理側へ位置を渡してから重ねる。
        Physics2D.SyncTransforms();
        while (playing && nextTick < duration && elapsed >= nextTick)
        {
            AttackHitQuery.Overlap(hitCollider, overlap);
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
        float diameter = radius * 2f;
        var scaleSprite = fill != null ? fill.sprite : null;
        transform.localScale = AttackVisual.ScaleTo(scaleSprite, new Vector2(diameter, diameter));

        if (fill != null && fill.transform != transform)
            fill.transform.localScale = visualBaseScale * (appear * pulse);

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
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
                fill = renderers[0];
            else
            {
                fill = gameObject.AddComponent<SpriteRenderer>();
                fill.sprite = AttackVisual.Diamond;
                fill.sortingOrder = 12;
            }
        }

        if (!visualScaleCaptured && fill != null && fill.transform != transform)
        {
            visualBaseScale = fill.transform.localScale;
            visualScaleCaptured = true;
        }

        if (fillMaterial != null && fill.sharedMaterial != fillMaterial)
            fill.sharedMaterial = fillMaterial;

        CacheHitCollider();

        if (hitCollider == null && !warnedMissingHit)
        {
            warnedMissingHit = true;
            Debug.LogWarning("エリア攻撃の子に、TriggerSender と Collider2D が必要です。", this);
        }

        UpdateVisual();
    }

    void CacheHitCollider()
    {
        if (hitCollider != null && hitCollider.enabled) return;

        hitCollider = null;
        if (hitSender == null) return;

        var colliders = hitSender.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || !colliders[i].enabled) continue;
            hitCollider = colliders[i];
            return;
        }
    }
}
