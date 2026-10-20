using UnityEngine;

/// <summary>
/// 攻撃時に確率で、画面内のランダムな位置に攻撃サークルを出す。
/// </summary>
public class AttackAreaCircle : AttackPatternBase
{
    [SerializeField] AreaCircle circlePrefab;
    [SerializeField] float procRate = 0.1f;
    [SerializeField] int prewarmCount = 4;
    [SerializeField] float duration = 5f;
    [SerializeField] float damageInterval = 0.5f;
    [SerializeField] float radius = 1.35f;
    [SerializeField] int generateCount = 1;

    AttackObjectPool<AreaCircle> pool;

    public override AttackPatternType PatternType => AttackPatternType.AreaCircle;

    public override void Begin(AttackUnitData data)
    {
        base.Begin(data);
        if (data != null)
        {
            procRate = data.attackRate;
            if (data.generateCount > 0) generateCount = data.generateCount;
            if (data.generateRate > 0f) damageInterval = data.generateRate;
            if (data.size > 0f) radius = data.size;
            if (data.duration > 0f) duration = data.duration;
        }

        if (circlePrefab == null)
        {
            Debug.LogWarning("攻撃サークルのプレハブが未設定です。");
            return;
        }

        pool = new AttackObjectPool<AreaCircle>(circlePrefab, transform, prewarmCount);
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
        for (int i = 0; i < count; i++)
        {
            if (!TryPickPosition(out var position)) return;
            var circle = pool.Rent();
            circle.Play(position, radius, duration, damageInterval, damage, pool.Return);
        }
    }

    bool TryPickPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (!AttackPlayfield.TryGetRect(out var rect)) return false;

        float minX = rect.xMin + radius;
        float maxX = rect.xMax - radius;
        float minY = Mathf.Max(rect.yMin + radius, -3.4f);
        float maxY = rect.yMax - radius;
        if (minX >= maxX || minY >= maxY) return false;

        position = new Vector3(
            UnityEngine.Random.Range(minX, maxX),
            UnityEngine.Random.Range(minY, maxY),
            0f);
        return true;
    }
}
