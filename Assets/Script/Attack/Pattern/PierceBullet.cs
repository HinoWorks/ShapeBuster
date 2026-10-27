using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 横方向へ飛ぶ貫通弾。当たった敵は 1 発につき 1 回だけダメージを受ける。
/// </summary>
public class PierceBullet : MonoBehaviour
{
    readonly List<IDamagable> overlap = new();
    readonly HashSet<IDamagable> hit = new();

    [SerializeField] Sprite sprite;
    [SerializeField] Color color = new Color(1f, 0.9f, 0.35f, 1f);
    [SerializeField] Vector2 visualSize = new Vector2(0.9f, 0.16f);
    [SerializeField] float spinSpeed = 720f;

    Action<PierceBullet> release;
    Vector2 hitSize;
    Vector3 velocity;
    float spin;
    int damage;
    int attackIndex = -1;
    bool playing;

    public void Launch(Vector3 position, int direction, float speed, int damage, Vector2 hitSize, Action<PierceBullet> release, int attackIndex = -1)
    {
        this.release = release;
        this.attackIndex = attackIndex;
        int sign = direction >= 0 ? 1 : -1;
        velocity = Vector3.right * (sign * speed);
        spin = -sign * spinSpeed;
        this.damage = damage;
        this.hitSize = hitSize;
        playing = true;
        hit.Clear();

        position.z = 0f;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        gameObject.SetActive(true);
        EnsureVisual();
    }

    void Update()
    {
        if (!playing) return;

        transform.position += velocity * Time.deltaTime;
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        AttackHitQuery.Box(transform.position, hitSize, overlap);
        for (int i = 0; i < overlap.Count; i++)
        {
            var target = overlap[i];
            if (!hit.Add(target)) continue;
            if (!target.isAlive) continue;
            target.Damage(damage, attackIndex: attackIndex, enemiesInArea: overlap.Count);
        }

        if (!playing) return;
        if (IsOutside())
            Finish();
    }

    void Finish()
    {
        if (!playing) return;
        playing = false;
        hit.Clear();
        release?.Invoke(this);
    }

    bool IsOutside()
    {
        if (!AttackPlayfield.TryGetRect(out var rect)) return false;
        float x = transform.position.x;
        return x < rect.xMin - 1.2f || x > rect.xMax + 1.2f;
    }

    void EnsureVisual()
    {
        if (!TryGetComponent(out SpriteRenderer renderer))
            renderer = gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = AttackVisual.Resolve(sprite, false);
        renderer.color = color;
        renderer.sortingOrder = 40;
        renderer.flipX = false;
        transform.localScale = AttackVisual.ScaleTo(renderer.sprite, visualSize);
    }
}
