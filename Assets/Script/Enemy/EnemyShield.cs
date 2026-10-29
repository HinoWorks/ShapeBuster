using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生成位置に留まる盾。普通の敵より手前に描き、横幅の下にいる敵のダメージを代わりに受ける。
/// 体力が 0 になると壊れる。動かないので、デッドラインでプレイヤーの体力は減らない。
/// </summary>
public class EnemyShield : EnemyBase
{
    static readonly List<EnemyShield> active = new();

    Collider2D cover;

    protected override bool RecoverVelocityYEnabled => false;

    protected override void OnSpawn()
    {
        MoveSpeed = 0f;

        var body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (cover == null)
            cover = GetComponent<Collider2D>();
        if (cover != null)
            cover.isTrigger = true;

        if (!active.Contains(this))
            active.Add(this);
    }

    protected override void TickMovement()
    {
        MoveSpeed = 0f;
    }

    protected override void OnRelease()
    {
        active.Remove(this);
    }

    void OnDestroy()
    {
        active.Remove(this);
    }

    /// <summary>動かないため、画面下へ到達してもプレイヤーの体力は減らさない。</summary>
    protected override void OnTriggerEnter2D(Collider2D other) { }

    /// <summary>
    /// 盾の下にいる敵なら、そのダメージを一番近い盾へ移す。移したら true。
    /// </summary>
    public static bool TryAbsorb(EnemyBase target, int damage, float resourceUpRate, int attackIndex, int enemiesInArea)
    {
        if (target == null || target is EnemyShield) return false;

        var shield = FindCover(target.GetTransform().position);
        if (shield == null) return false;

        shield.Damage(damage, resourceUpRate, attackIndex, enemiesInArea);
        return true;
    }

    static EnemyShield FindCover(Vector2 point)
    {
        EnemyShield best = null;
        float bestY = float.PositiveInfinity;
        for (int i = 0; i < active.Count; i++)
        {
            var shield = active[i];
            if (shield == null || !shield.isAlive) continue;
            if (!shield.Covers(point)) continue;

            float y = shield.transform.position.y;
            if (y >= bestY) continue;
            bestY = y;
            best = shield;
        }
        return best;
    }

    /// <summary>横幅の内側で、盾の上端より下にいる点を守る。</summary>
    bool Covers(Vector2 point)
    {
        if (cover == null)
            cover = GetComponent<Collider2D>();
        if (cover == null) return false;

        var bounds = cover.bounds;
        if (point.x < bounds.min.x || point.x > bounds.max.x) return false;
        return point.y <= bounds.max.y;
    }
}
