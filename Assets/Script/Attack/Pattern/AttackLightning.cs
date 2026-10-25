using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻撃時に確率で、生存中の敵へ電撃を飛ばす。
/// </summary>
public class AttackLightning : AttackPatternBase
{
    [SerializeField] LightningBolt boltPrefab;
    [SerializeField] float procRate = 0.3f;
    [SerializeField] int prewarmCount = 6;
    [SerializeField] float boltDuration = 0.18f;
    [SerializeField] int generateCount = 1;

    readonly List<EnemyBase> struck = new();
    AttackObjectPool<LightningBolt> pool;

    public override AttackPatternType PatternType => AttackPatternType.Lightning;

    public override void Begin(AttackUnitData data)
    {
        base.Begin(data);
        if (data != null)
        {
            procRate = data.attackRate;
            if (data.generateCount > 0) generateCount = data.generateCount;
            if (data.duration > 0f) boltDuration = data.duration;
        }

        if (boltPrefab == null)
        {
            Debug.LogWarning("電撃のプレハブが未設定です。");
            return;
        }

        pool = new AttackObjectPool<LightningBolt>(boltPrefab, transform, prewarmCount);
        ListenToAttacks(OnAttack);
    }

    public override void End()
    {
        base.End();
        pool?.ReleaseAll();
    }

    void OnAttack()
    {
        if (pool == null || EnemyGenerateManager.Inst == null) return;
        if (!EnemyGenerateManager.Inst.TryPickRandomAlive(out _)) return;
        if (!Roll(procRate)) return;

        struck.Clear();
        int count = Mathf.Max(1, generateCount);
        int damage = CurrentDamage();
        var origin = AttackOrigin();
        for (int i = 0; i < count; i++)
        {
            if (!TryPickUnstruck(out var enemy)) break;
            struck.Add(enemy);
            var target = enemy.GetTransform();
            if (target == null || !enemy.isAlive) continue;

            enemy.Damage(damage, attackIndex: Unit != null ? Unit.index : (int)AttackPatternType.Lightning);
            var bolt = pool.Rent();
            bolt.Play(origin, target.position, boltDuration, pool.Return);
        }
    }

    bool TryPickUnstruck(out EnemyBase enemy)
    {
        enemy = null;
        for (int i = 0; i < 8; i++)
        {
            if (!EnemyGenerateManager.Inst.TryPickRandomAlive(out enemy)) return false;
            if (!struck.Contains(enemy)) return true;
        }
        return false;
    }
}
