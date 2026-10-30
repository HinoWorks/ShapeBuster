using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カーソルを中心に置き、線の幅が太くなるレーザー。接触した敵は 1 本につき 1 回ダメージを受ける。
/// 当たりの形は子の Collider2D。ルートのスケールだけ長さと幅に合わせる。
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
    [SerializeField] TriggerSender hitSender;
    [SerializeField] Material beamMaterial;

    Action<LaserBeam> release;
    SpriteRenderer beamRenderer;
    Collider2D hitCollider;
    bool warnedMissingHit;
    Vector3 origin;
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
        float beamLength = Mathf.Max(0.1f, length);
        origin.z = 0f;
        var visual = beamRenderer != null && beamRenderer.sprite != null
            ? beamRenderer.sprite
            : AttackVisual.Resolve(sprite, false);
        var scale = AttackVisual.ScaleTo(visual, new Vector2(beamLength, nextWidth));
        transform.localScale = scale;

        var center = visual != null ? visual.bounds.center : Vector3.zero;
        if (beamRenderer != null && beamRenderer.transform != transform)
        {
            var child = beamRenderer.transform;
            center.x *= child.localScale.x;
            center.y *= child.localScale.y;
            center = child.localRotation * center + child.localPosition;
        }
        center.x *= scale.x;
        center.y *= scale.y;
        transform.position = origin - transform.rotation * center;
    }

    void DamageOverlaps()
    {
        if (hitCollider == null) return;

        // 幅を変えた直後は物理側へスケールを渡してから重ねる。
        Physics2D.SyncTransforms();
        AttackHitQuery.Overlap(hitCollider, overlap);
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
        if (beamRenderer == null)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
                beamRenderer = renderers[0];
            else if (hitSender != null)
                beamRenderer = hitSender.gameObject.AddComponent<SpriteRenderer>();
            else
                beamRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        beamRenderer.sprite = AttackVisual.Resolve(sprite, false);
        beamRenderer.color = color;
        beamRenderer.sortingOrder = 28;
        if (beamMaterial != null && beamRenderer.sharedMaterial != beamMaterial)
            beamRenderer.sharedMaterial = beamMaterial;
        beamRenderer.flipX = false;
        beamRenderer.flipY = false;

        CacheHitCollider();
        if (hitCollider == null && !warnedMissingHit)
        {
            warnedMissingHit = true;
            Debug.LogWarning("レーザーの子に、TriggerSender と Collider2D が必要です。", this);
        }
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
