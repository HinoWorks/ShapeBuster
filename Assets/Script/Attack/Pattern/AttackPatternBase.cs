using System;
using UniRx;
using UnityEngine;

/// <summary>
/// スキルごとに出す攻撃本体。ゲーム開始時に生成し、終了時に破棄する。
/// </summary>
public abstract class AttackPatternBase : MonoBehaviour
{
    IDisposable attackSubscription;

    public abstract AttackPatternType PatternType { get; }

    protected AttackUnitData Unit { get; private set; }

    public virtual void Begin(AttackUnitData data)
    {
        Unit = data;
    }

    public virtual void End()
    {
        attackSubscription?.Dispose();
        attackSubscription = null;
    }

    void OnDestroy()
    {
        End();
    }

    protected void ListenToAttacks(Action onAttack)
    {
        attackSubscription?.Dispose();
        if (AttackManager.Inst == null) return;
        attackSubscription = AttackManager.Inst.OnPickaxeAttackTiming.Subscribe(_ => onAttack());
    }

    protected int CurrentDamage()
    {
        float baseDamage = Unit != null ? Unit.baseDamage : 0f;
        return SkillTreeBonus.RoundDamage(baseDamage);
    }

    protected Vector3 AttackOrigin()
    {
        var cursor = AttackManager.Inst != null ? AttackManager.Inst.CursorTransform : null;
        if (cursor == null) return Vector3.zero;
        var position = cursor.position;
        position.z = 0f;
        return position;
    }

    protected static bool Roll(float rate) => UnityEngine.Random.value < rate;
}
