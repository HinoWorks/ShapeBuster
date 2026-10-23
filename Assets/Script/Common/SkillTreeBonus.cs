using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スキルごとの効果は「1レベル分の差分 × 現在レベル」で置き換える。
/// 起動時の復元と強化の両方がこれを通るので、同じ効果が二重に足されない。
/// </summary>
public static class SkillTreeBonus
{
    struct Contribution
    {
        public SkillParamTarget target;
        public int targetIndex;
        public SkillParamStat stat;
        public float value;
        public SkillParamStat stat2;
        public float value2;
    }

    static readonly Dictionary<int, Contribution> bySkill = new();

    public static int CursorDamage => Mathf.RoundToInt(Sum(SkillParamTarget.Cursor, SkillParamStat.Damage));
    public static float CursorAttackInterval => Sum(SkillParamTarget.Cursor, SkillParamStat.AttackInterval);
    public static float CursorCriticalRate => Sum(SkillParamTarget.Cursor, SkillParamStat.CriticalRate);
    public static float CursorSize => Sum(SkillParamTarget.Cursor, SkillParamStat.Size);
    public static float CursorResourceUpRate => Sum(SkillParamTarget.Cursor, SkillParamStat.ResourceUpRate);
    public static int PlayerMaxHp => Mathf.RoundToInt(Sum(SkillParamTarget.Player, SkillParamStat.MaxHp));

    public static void Clear()
    {
        bySkill.Clear();
    }

    public static void SetLevel(int skillIndex, SkillTreeBase skill, int level)
    {
        if (skill == null || level <= 0)
        {
            bySkill.Remove(skillIndex);
            return;
        }

        bySkill[skillIndex] = new Contribution
        {
            target = skill.paramTarget,
            targetIndex = skill.targetIndex,
            stat = skill.paramStat,
            value = skill.deltaValue * level,
            stat2 = skill.paramStat2,
            value2 = skill.deltaValue2 * level
        };
    }

    public static AttackUnitData WithAttackBonus(AttackUnitData source)
    {
        if (source == null) return null;

        return new AttackUnitData
        {
            index = source.index,
            attackName = source.attackName,
            pf = source.pf,
            attackRate = source.attackRate + AttackValue(source.index, SkillParamStat.AttackRate),
            generateRate = source.generateRate + AttackValue(source.index, SkillParamStat.GenerateRate),
            generateCount = Mathf.Max(0, source.generateCount + Mathf.RoundToInt(AttackValue(source.index, SkillParamStat.GenerateCount))),
            size = source.size + AttackValue(source.index, SkillParamStat.Size),
            speed = source.speed + AttackValue(source.index, SkillParamStat.Speed),
            duration = source.duration + AttackValue(source.index, SkillParamStat.Duration)
        };
    }

    public static float AttackValue(int attackIndex, SkillParamStat stat)
    {
        if (stat == SkillParamStat.None) return 0f;

        float total = 0f;
        foreach (var contribution in bySkill.Values)
        {
            if (contribution.target != SkillParamTarget.Attack) continue;
            if (contribution.targetIndex != attackIndex) continue;
            if (contribution.stat == stat) total += contribution.value;
            if (contribution.stat2 == stat) total += contribution.value2;
        }
        return total;
    }

    static float Sum(SkillParamTarget target, SkillParamStat stat)
    {
        if (stat == SkillParamStat.None) return 0f;

        float total = 0f;
        foreach (var contribution in bySkill.Values)
        {
            if (contribution.target != target) continue;
            if (contribution.stat == stat) total += contribution.value;
            if (contribution.stat2 == stat) total += contribution.value2;
        }
        return total;
    }
}
