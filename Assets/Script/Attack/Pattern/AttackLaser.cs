using UnityEngine;

/// <summary>
/// 攻撃時に確率で、カーソル位置を中心にランダムな角度へ太いレーザーを出す。
/// </summary>
public class AttackLaser : AttackPatternBase
{
    [SerializeField] LaserBeam beamPrefab;
    [SerializeField] float procRate = 0.2f;
    [SerializeField] int prewarmCount = 4;
    [SerializeField] int generateCount = 1;
    [SerializeField] float maxWidth = 1.15f;
    [SerializeField] float growDuration = 0.18f;

    AttackObjectPool<LaserBeam> pool;

    public override AttackPatternType PatternType => AttackPatternType.Laser;

    public override void Begin(AttackUnitData data)
    {
        base.Begin(data);
        if (data != null)
        {
            procRate = data.attackRate;
            if (data.generateCount > 0) generateCount = data.generateCount;
            if (data.size > 0f) maxWidth = data.size;
            if (data.generateRate > 0f) growDuration = data.generateRate;
        }

        if (beamPrefab == null)
        {
            Debug.LogWarning("レーザーのプレハブが未設定です。");
            return;
        }

        pool = new AttackObjectPool<LaserBeam>(beamPrefab, transform, prewarmCount);
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

        int count = Mathf.Max(1, generateCount);
        int damage = CurrentDamage();
        var origin = AttackOrigin();
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f);
            var beam = pool.Rent();
            beam.Play(origin, angle, damage, maxWidth, growDuration, pool.Return);
        }
    }
}
