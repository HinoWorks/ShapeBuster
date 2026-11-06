using System;
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
    }

    static readonly Dictionary<int, Contribution> bySkill = new();
    static float armorRemainder;
    static float reloadGuardRemainder;

    public static int CursorDamage => Mathf.RoundToInt(Sum(SkillParamTarget.Cursor, SkillParamStat.Damage));
    public static float CursorAttackInterval => Sum(SkillParamTarget.Cursor, SkillParamStat.AttackInterval);
    public static float CursorCriticalRate => Sum(SkillParamTarget.Cursor, SkillParamStat.CriticalRate);
    public static float CursorSize => Sum(SkillParamTarget.Cursor, SkillParamStat.Size);
    public static float CursorResourceUpRate => Sum(SkillParamTarget.Cursor, SkillParamStat.ResourceUpRate);
    public static float ConditionalDamage => Sum(SkillParamTarget.Cursor, SkillParamStat.ConditionalDamage);
    public static float CriticalDamage => Sum(SkillParamTarget.Cursor, SkillParamStat.CriticalDamage);
    public static float BossDamage => Sum(SkillParamTarget.Cursor, SkillParamStat.BossDamage);
    public static float PickupRange => Sum(SkillParamTarget.Cursor, SkillParamStat.PickupRange);
    public static float BulletRegain => Sum(SkillParamTarget.Cursor, SkillParamStat.BulletRegain);
    public static float ReloadReduction => Sum(SkillParamTarget.Cursor, SkillParamStat.ReloadReduction);
    public static float ReloadTimeScale => Mathf.Max(0f, 1f - Mathf.Max(0f, ReloadReduction));
    public static int MagazineAmmoAdd => Mathf.RoundToInt(Sum(SkillParamTarget.Cursor, SkillParamStat.MagazineAmmo));
    public static float MissRefund => Sum(SkillParamTarget.Cursor, SkillParamStat.MissRefund);
    public static float StoppedAmmoRegen => Sum(SkillParamTarget.Cursor, SkillParamStat.StoppedAmmoRegen);
    public static float ReloadGuard => Sum(SkillParamTarget.Cursor, SkillParamStat.ReloadGuard);
    public static int PlayerMaxHp => Mathf.RoundToInt(Sum(SkillParamTarget.Player, SkillParamStat.MaxHp));
    public static float Regen => Sum(SkillParamTarget.Player, SkillParamStat.Regen);
    public static float Armor => Sum(SkillParamTarget.Player, SkillParamStat.Armor);
    public static float SpawnRate => Sum(SkillParamTarget.Player, SkillParamStat.SpawnRate);
    public static float GlassSpawnRate => Mathf.Clamp01(Sum(SkillParamTarget.Player, SkillParamStat.GlassSpawnRate));

    public const float BlockCellShrinkPerLevel = 0.06f;
    public const float BlockResourcePerLevel = 0.12f;

    public static float BlockCompactLevel => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockCompact));

    /// <summary>1で元の大きさ。1段ごとに一辺が6%小さくなる。</summary>
    public static float BlockCellScale => Mathf.Pow(1f - BlockCellShrinkPerLevel, BlockCompactLevel);

    /// <summary>ブロック破壊資源への加算倍率。1段で +12%。</summary>
    public static float BlockResourceUpRate => BlockCompactLevel * BlockResourcePerLevel;

    /// <summary>ステージの列数を基準に、縮んだマスが同じ幅へ並ぶ列数へ増やす。</summary>
    public static int ColumnsFor(int baseColumns)
    {
        int columns = Mathf.Max(1, baseColumns);
        float scale = Mathf.Clamp(BlockCellScale, 0.4f, 1f);
        if (scale >= 0.999f) return columns;
        return Mathf.Max(columns, Mathf.RoundToInt(columns / scale));
    }

    public static string FormatBlockCompact(float level)
    {
        float scale = Mathf.Pow(1f - BlockCellShrinkPerLevel, Mathf.Max(0f, level));
        float shrink = (1f - scale) * 100f;
        float resource = Mathf.Max(0f, level) * BlockResourcePerLevel * 100f;
        return $"サイズ -{shrink:F0}%  資源 +{resource:F0}%";
    }

    public static bool OwnsBall(BlockBallKind kind)
    {
        int targetIndex;
        switch (kind)
        {
            case BlockBallKind.Bomb:
                targetIndex = 0;
                break;
            case BlockBallKind.Laser:
                targetIndex = 1;
                break;
            case BlockBallKind.LaserRow:
                targetIndex = 3;
                break;
            case BlockBallKind.Heavy:
                targetIndex = 2;
                break;
            case BlockBallKind.Orbit:
                targetIndex = 4;
                break;
            case BlockBallKind.Cross:
                targetIndex = 5;
                break;
            case BlockBallKind.Poison:
                targetIndex = 6;
                break;
            default:
                return false;
        }

        float total = 0f;
        foreach (var contribution in bySkill.Values)
        {
            if (contribution.target != SkillParamTarget.Ball) continue;
            if (contribution.targetIndex != targetIndex) continue;
            if (contribution.stat != SkillParamStat.BallUnlock) continue;
            total += contribution.value;
        }
        return total >= 1f;
    }

    public const float ArmorCap = 0.8f;

    public static int ScaleDealtDamage(int damage, int enemiesInArea, bool isBoss)
    {
        if (damage <= 0) return 0;

        float scale = 1f + Mathf.Max(0f, ConditionalDamage) * Mathf.Max(0, enemiesInArea);
        if (isBoss)
            scale *= 1f + Mathf.Max(0f, BossDamage);
        return Mathf.Max(1, Mathf.RoundToInt(damage * scale));
    }

    public static int ApplyArmor(int damage)
    {
        return ApplyFractionalReduction(damage, Armor, ref armorRemainder);
    }

    public static int ApplyReloadGuard(int damage)
    {
        return ApplyFractionalReduction(damage, ReloadGuard, ref reloadGuardRemainder);
    }

    static int ApplyFractionalReduction(int damage, float reduction, ref float remainder)
    {
        if (damage <= 0) return 0;

        reduction = Mathf.Clamp(reduction, 0f, ArmorCap);
        float reduced = damage * (1f - reduction);
        int applied = Mathf.FloorToInt(reduced);
        remainder += reduced - applied;
        if (remainder >= 1f)
        {
            int carry = Mathf.FloorToInt(remainder);
            applied += carry;
            remainder -= carry;
        }
        return applied;
    }

    public static void Clear()
    {
        bySkill.Clear();
        armorRemainder = 0f;
        reloadGuardRemainder = 0f;
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
            value = skill.deltaValue * level
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
            baseDamage = source.baseDamage + AttackValue(source.index, SkillParamStat.Damage),
            generateRate = source.generateRate + AttackValue(source.index, SkillParamStat.GenerateRate),
            generateCount = Mathf.Max(0, source.generateCount + Mathf.RoundToInt(AttackValue(source.index, SkillParamStat.GenerateCount))),
            size = source.size + AttackValue(source.index, SkillParamStat.Size),
            speed = source.speed + AttackValue(source.index, SkillParamStat.Speed),
            duration = source.duration + AttackValue(source.index, SkillParamStat.Duration)
        };
    }

    public static int RoundDamage(float value)
    {
        if (value <= 0f) return 0;
        return Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));
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
        }
        return total;
    }
}
