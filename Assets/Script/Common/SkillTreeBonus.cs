using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>1ヒット分のダメージと、そのヒットがクリティカルだったか。</summary>
public struct BallHitRoll
{
    public int damage;
    public bool critical;
}

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

    public static float GlassSpawnRate => Mathf.Clamp01(Sum(SkillParamTarget.Player, SkillParamStat.GlassSpawnRate));

    public const float BlockCellShrinkPerLevel = 0.06f;
    public const float BlockResourcePerLevel = 0.12f;

    /// <summary>More Block。1レベルで列を1つ増やす。</summary>
    public static float BlockCompactLevel => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockCompact));

    public static int AddedColumns => Mathf.Max(0, Mathf.RoundToInt(BlockCompactLevel));

    /// <summary>縮小スキル用。今はどのスキルも入れないので 0。</summary>
    public static float BlockShrinkLevel => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockShrink));

    /// <summary>1で元の大きさ。1段ごとに一辺が6%小さくなる。</summary>
    public static float BlockCellScale => Mathf.Pow(1f - BlockCellShrinkPerLevel, BlockShrinkLevel);

    /// <summary>ブロック破壊資源への加算倍率。1段で +12%。縮小スキル用。</summary>
    public static float BlockResourceUpRate => BlockShrinkLevel * BlockResourcePerLevel;

    public static int NormalBallCount => 1 + Mathf.RoundToInt(Sum(SkillParamTarget.Ball, SkillParamStat.BallCount)) + MilestoneBonus.BallCount;
    public static float BallSpeedScale => (1f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallSpeed))) * (1f + MilestoneBonus.BallSpeed);
    public static float FallSpeedScale => Mathf.Max(0.4f, 1f + Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockFallFast)));
    public static float BlockHarvestRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockHarvest));
    /// <summary>ブロックが落とすAが2倍になる確率（0〜1）。</summary>
    public static float BlockDoubleDropChance => Mathf.Clamp01(Sum(SkillParamTarget.Block, SkillParamStat.BlockDoubleDrop));
    public static float BlockMagnetRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.BlockMagnet));
    public static bool BlockLinger => Sum(SkillParamTarget.Block, SkillParamStat.BlockLinger) >= 1f;
    public static float RegenSpawnRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.RegenSpawn));
    public static float ArmorSpawnRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.ArmorSpawn));
    public static int RegenDropAdd => Mathf.RoundToInt(Sum(SkillParamTarget.Block, SkillParamStat.RegenDrop));
    public static int ArmorDropAdd => Mathf.RoundToInt(Sum(SkillParamTarget.Block, SkillParamStat.ArmorDrop));

    /// <summary>宝箱の生成重み（通常群に対する割合）。0 なら宝箱は出ない。</summary>
    public static float ChestSpawnRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.ChestSpawn));
    /// <summary>宝箱が落とす A への加算倍率。0.25 で +25%。</summary>
    public static float ChestRewardRate => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.ChestReward));
    /// <summary>壁の加速帯が出る（間隔のスキルを 1 Lv 以上取った）。</summary>
    public static bool WallBoostUnlocked => Has(SkillParamTarget.Block, SkillParamStat.WallBoostInterval);
    /// <summary>壁の加速帯が出る間隔（秒）。</summary>
    public static float WallBoostInterval => WallBoostIntervalFor(Sum(SkillParamTarget.Block, SkillParamStat.WallBoostInterval));
    /// <summary>加速帯で上がる速さへの加算。SO_BlockSession の wallBoostRate（素の倍率）に足す。</summary>
    public static float WallBoostPower => Mathf.Max(0f, Sum(SkillParamTarget.Block, SkillParamStat.WallBoostPower));

    /// <summary>間隔のスキルの合計値（負の秒）から、実際の間隔を出す。スキルの説明の表示にも使う。</summary>
    public static float WallBoostIntervalFor(float skillValue)
    {
        var rule = SOLoader.BlockSession;
        float baseInterval = rule != null ? rule.wallBoostBaseInterval : 22f;
        float min = rule != null ? rule.wallBoostMinInterval : 4f;
        return Mathf.Max(min, baseInterval + skillValue);
    }
    public static bool ConvertUnlocked => Sum(SkillParamTarget.Block, SkillParamStat.ConvertUnlock) >= 1f;
    public static bool MilestoneUnlocked => Sum(SkillParamTarget.Block, SkillParamStat.MilestoneUnlock) >= 1f;
    /// <summary>ゾンビ図鑑のタブを出す（スキルツリーの「ゾンビ図鑑」）。発見の記録は、開く前から残る。</summary>
    public static bool BestiaryUnlocked => Sum(SkillParamTarget.Block, SkillParamStat.BestiaryUnlock) >= 1f;
    /// <summary>ワクチンのタブを出す（スキルツリーの「ワクチン研究所」）。</summary>
    public static bool VaccineLabUnlocked => Sum(SkillParamTarget.Block, SkillParamStat.VaccineLabUnlock) >= 1f;
    public static bool OwnsTurret => Sum(SkillParamTarget.Player, SkillParamStat.TurretUnlock) >= 1f;
    public static bool OwnsRowBeam => Sum(SkillParamTarget.Player, SkillParamStat.RowBeamUnlock) >= 1f;
    public static float TurretDamageRate => Mathf.Max(0f, Sum(SkillParamTarget.Player, SkillParamStat.TurretDamage));
    public static float TurretFireRate => Mathf.Max(0f, Sum(SkillParamTarget.Player, SkillParamStat.TurretFireRate));
    public static int TurretExtraShots => Mathf.Max(0, Mathf.RoundToInt(Sum(SkillParamTarget.Player, SkillParamStat.TurretExtra)));
    public static float RowBeamDamageRate => Mathf.Max(0f, Sum(SkillParamTarget.Player, SkillParamStat.RowBeamDamage));
    public static float RowBeamFireRate => Mathf.Max(0f, Sum(SkillParamTarget.Player, SkillParamStat.RowBeamInterval));

    public static int BallCopies(BlockBallKind kind)
    {
        int index = BallTarget(kind);
        if (index < 0) return 0;
        if (!OwnsBall(kind)) return 0;
        int copies = 1;
        if (SumBall(SkillParamStat.BallExtra, index) >= 1f && SumBall(SkillParamStat.BallRatio, index) > 0f)
            copies++;
        return copies;
    }

    public static float HeavySpeedScale => 0.3f + Mathf.Max(0f, SumBall(SkillParamStat.BallHeavySpeed, 2)) + MilestoneBonus.HeavySpeed;
    public static int OrbitCount => 3 + Mathf.RoundToInt(SumBall(SkillParamStat.BallOrbitCount, 4)) + MilestoneBonus.OrbitCount;
    public static bool ChainHits => Sum(SkillParamTarget.Ball, SkillParamStat.BallChain) >= 1f;
    /// <summary>連撃で、1 ヒットごとに次のヒットが強くなる割合。</summary>
    public const float ChainStep = 0.1f;
    /// <summary>連撃の強さの上限。素の 50% に、連撃＋（BallChainCap）を足す。</summary>
    public static float ChainCap => 0.5f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallChainCap));
    public static float FirstAttackRate => Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallFirstAttack));
    public static float LastAttackRate => Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallLastAttack));
    public static float FullStackRate => Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallFullStack));
    public static bool CatchReturning => Sum(SkillParamTarget.Ball, SkillParamStat.BallCatch) >= 1f;
    public const float LastAttackHp = 0.25f;

    /// <summary>無傷、残り25%以下、全回収発射の倍率。掛かるものは掛け合わせる。</summary>
    public static float StrikeScale(BlockPiece piece, bool fullStack)
    {
        float scale = 1f;
        if (piece != null && piece.Unhurt)
            scale *= 1f + FirstAttackRate;
        if (piece != null && piece.HpRatio <= LastAttackHp)
            scale *= 1f + LastAttackRate;
        if (fullStack)
            scale *= 1f + FullStackRate;
        // ゾンビ図鑑の研究：普通のゾンビとガラスへのダメージ。
        if (piece != null)
        {
            if (piece.BestiaryId == 1)
                scale *= 1f + BestiaryBonus.NormalDamage;
            else if (piece.ReleasesContents)
                scale *= 1f + BestiaryBonus.GlassDamage;
        }
        return scale;
    }

    public static float ProcChance(BlockBallKind kind)
    {
        float rate = BaseProcRate(kind);
        int index = BallTarget(kind);
        if (index >= 0)
            rate += Mathf.Max(0f, SumBall(SkillParamStat.BallProc, index));
        return Mathf.Clamp01(rate + MilestoneBonus.Proc(kind));
    }

    /// <summary>Ball シートの baseProcRate。行が無いボム、レーザー、クロス、ポイズンは 0.1。</summary>
    static float BaseProcRate(BlockBallKind kind)
    {
        var table = SOLoader.BallData;
        var data = table != null ? table.Find(kind) : null;
        if (data != null)
            return Mathf.Max(0f, data.baseProcRate);
        switch (kind)
        {
            case BlockBallKind.Bomb:
            case BlockBallKind.Laser:
            case BlockBallKind.LaserRow:
            case BlockBallKind.Cross:
            case BlockBallKind.Poison:
                return 0.1f;
            default:
                return 0f;
        }
    }

    /// <summary>ノーマルの攻撃力に、その弾の素の比率を掛けた値。端数は最後にまとめて四捨五入する。</summary>
    public static float AttackPower(BlockBallKind kind)
    {
        float normal = (1f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallFlatDamage)))
            * (1f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallDamageRate)))
            * (1f + MilestoneBonus.BallDamageRate)
            * (1f + BestiaryBonus.AllDamage);
        float ratio = BaseDamageRate(kind);
        if (kind == BlockBallKind.Normal)
            return normal * ratio * (1f + MilestoneBonus.KindDamage(kind));

        int index = BallTarget(kind);
        if (index >= 0)
            ratio += Mathf.Max(0f, SumBall(SkillParamStat.BallRatio, index));
        float power = normal * ratio;
        if (kind == BlockBallKind.Turret)
            power *= (1f + TurretDamageRate) * (1f + MilestoneBonus.KindDamage(kind));
        else if (kind == BlockBallKind.AutoBeam)
            power *= (1f + RowBeamDamageRate) * (1f + MilestoneBonus.KindDamage(kind));
        return power;
    }

    /// <summary>Ball シートの baseDamageRate。未設定の特殊弾は 0.5、ノーマルは 1。</summary>
    static float BaseDamageRate(BlockBallKind kind)
    {
        var table = SOLoader.BallData;
        var data = table != null ? table.Find(kind) : null;
        if (data != null && data.baseDamageRate > 0f)
            return data.baseDamageRate;
        return kind == BlockBallKind.Normal ? 1f : 0.5f;
    }

    public static BallHitRoll RollHit(BlockBallKind kind, int hitsBefore, bool boss)
    {
        float power = AttackPower(kind);
        if (ChainHits && hitsBefore > 0)
            power *= 1f + Mathf.Min(ChainCap, ChainStep * hitsBefore);
        bool critical = false;
        if (UnityEngine.Random.value < Mathf.Clamp01(Sum(SkillParamTarget.Ball, SkillParamStat.BallCritRate) + MilestoneBonus.BallCritRate))
        {
            power *= 2f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallCritDamage)) + MilestoneBonus.BallCritDamage;
            critical = true;
            GameEvent.InGame.PublishCriticalHit();
        }
        if (boss)
            power *= 1f + Mathf.Max(0f, Sum(SkillParamTarget.Ball, SkillParamStat.BallBoss)) + MilestoneBonus.BallBoss;
        return new BallHitRoll
        {
            damage = Mathf.Max(1, RoundDamage(power)),
            critical = critical,
        };
    }

    static int BallTarget(BlockBallKind kind)
    {
        switch (kind)
        {
            case BlockBallKind.Bomb: return 0;
            case BlockBallKind.Laser: return 1;
            case BlockBallKind.Heavy: return 2;
            case BlockBallKind.LaserRow: return 3;
            case BlockBallKind.Orbit: return 4;
            case BlockBallKind.Cross: return 5;
            case BlockBallKind.Poison: return 6;
            default: return -1;
        }
    }

    static float SumBall(SkillParamStat stat, int targetIndex)
    {
        float total = 0f;
        foreach (var contribution in bySkill.Values)
        {
            if (contribution.target != SkillParamTarget.Ball) continue;
            if (contribution.targetIndex != targetIndex) continue;
            if (contribution.stat != stat) continue;
            total += contribution.value;
        }
        return total;
    }

    /// <summary>縮小で空いた同じ幅に並べる列数。More Block は使わない。</summary>
    public static int ColumnsFor(int baseColumns)
    {
        int columns = Mathf.Max(1, baseColumns);
        float scale = Mathf.Clamp(BlockCellScale, 0.4f, 1f);
        if (scale >= 0.999f) return columns;
        return Mathf.Max(columns, Mathf.RoundToInt(columns / scale));
    }

    public static string FormatBlockCompact(float level)
    {
        int columns = Mathf.RoundToInt(level);
        if (columns > 0) return "+" + columns;
        return columns.ToString();
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

    /// <summary>どれか 1 つでもスキルを覚えている（Lv 1 以上）。</summary>
    public static bool AnyLearned => bySkill.Count > 0;

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
            value = skill.deltaValue * level
        };
    }

    public static int RoundDamage(float value)
    {
        if (value <= 0f) return 0;
        return Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    /// <summary>その効果のスキルを 1 Lv 以上取っている（Lv 0 のスキルは bySkill に入らない）。</summary>
    static bool Has(SkillParamTarget target, SkillParamStat stat)
    {
        if (stat == SkillParamStat.None) return false;
        foreach (var contribution in bySkill.Values)
        {
            if (contribution.target == target && contribution.stat == stat) return true;
        }
        return false;
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
