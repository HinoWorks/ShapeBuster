using UnityEngine;

/// <summary>
/// 攻撃時に確率で、カーソルの高さから左右へ貫通弾を撃つ。
/// </summary>
public class AttackPierce : AttackPatternBase
{
    [SerializeField] PierceBullet bulletPrefab;
    [SerializeField] float procRate = 0.1f;
    [SerializeField] int prewarmCount = 6;
    [SerializeField] float speed = 14f;
    [SerializeField] float bulletLife = 1f;
    [SerializeField] Vector2 hitSize = new Vector2(0.9f, 0.7f);
    [SerializeField] int generateCount = 2;

    AttackObjectPool<PierceBullet> pool;

    public override AttackPatternType PatternType => AttackPatternType.Pierce;

    public override void Begin(AttackUnitData data)
    {
        base.Begin(data);
        if (data != null)
        {
            procRate = data.attackRate;
            if (data.generateCount > 0) generateCount = data.generateCount;
            if (data.speed > 0f) speed = data.speed;
            if (data.size > 0f) hitSize = new Vector2(data.size, data.size);
        }

        if (bulletPrefab == null)
        {
            Debug.LogWarning("貫通弾のプレハブが未設定です。");
            return;
        }

        pool = new AttackObjectPool<PierceBullet>(bulletPrefab, transform, prewarmCount);
        ListenToAttacks(OnAttack);
    }

    public override void End()
    {
        base.End();
        pool?.ReleaseAll();
    }

    void OnAttack()
    {
        if (pool == null || !Roll(procRate)) return;

        var origin = AttackOrigin();
        int damage = CurrentDamage();
        int count = Mathf.Max(1, generateCount);
        for (int i = 0; i < count; i++)
            Launch(origin, i % 2 == 0 ? 1 : -1, damage, Unit != null ? Unit.index : (int)AttackPatternType.Pierce);
    }

    void Launch(Vector3 origin, int direction, int damage, int attackIndex)
    {
        var bullet = pool.Rent();
        bullet.Launch(origin, direction, speed, bulletLife, damage, hitSize, pool.Return, attackIndex);
    }
}
