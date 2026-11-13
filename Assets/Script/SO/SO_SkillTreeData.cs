using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillParamTarget
{
    None,
    Cursor,
    Player,
    Attack,
    Ball,
    Block
}

public enum SkillParamStat
{
    // == Common ==
    None = 0,

    // == Ball ==
    BallUnlock = 28, // 特殊ボールを解放。targetIndex はボム0、縦レーザー1、ヘビー2、横レーザー3、オービット4、クロス5、ポイズン6
    BallCount = 30, // ノーマル弾の本数
    BallSpeed = 31, // 全ボールの速さへの倍率。0.15 で +15%
    BallFlatDamage = 32, // ノーマル攻撃力への加算
    BallDamageRate = 35, // ノーマル攻撃力への倍率
    BallCritRate = 33, // ボールのクリティカル率
    BallCritDamage = 34, // クリティカル倍率への加算。素の2倍に足す
    BallChain = 36, // 1以上で、同じフライトの連続ヒットが次弾を強くする
    BallBoss = 37, // ボスへの攻撃力倍率

    // == Ball : 特殊弾 ==
    BallProc = 38, // 特殊弾の発生率への加算。targetIndex はボム0から
    BallRatio = 39, // 特殊弾の素の比率へ足す分。シートの baseDamageRate が 0.5 なら、0.5 で 100% になる
    BallExtra = 40, // その特殊弾の本数
    BallHeavySpeed = 41, // ヘビーの速さの割合への加算。素の30%に足す
    BallOrbitCount = 42, // オービットの周回の数

    // == Block ==
    BlockCompact = 29, // 1レベルで一辺6%縮小し、ステージ列数から列を増やす。破壊資源は1レベル+12%
    BlockFallSlow = 43, // ブロック下降を遅くする割合
    BlockHarvest = 44, // ブロックが落とすAへの倍率
    BlockMagnet = 45, // 拾得半径への倍率
    BlockLinger = 46, // 1以上で、資源がデッドラインに残る
    RegenSpawn = 47, // 再生ブロックの生成重み。0.04 で通常群の4%
    ArmorSpawn = 48, // 壁ブロックの生成重み
    RegenDrop = 49, // 再生ブロックの固有ドロップへの加算
    ArmorDrop = 50, // 壁ブロックの固有ドロップへの加算

    // == Convert ==
    ConvertUnlock = 51, // 1以上で Convert タブを出す
    ConvertRate = 52, // Convert の必要AAを減らす段

    // == Player ==
    GlassSpawnRate = 27, // 敵が1体出るときの、ガラスカプセルの出現率。0.05 で 5%
    TurretUnlock = 53, // 1以上で、左右の下に固定砲台が付く
    RowBeamUnlock = 54, // 1以上で、一番下の段へ横ビームを撃つ
}

[Serializable]
public class SkillTreeBase
{
    public int baseSkillIndex; //スキル定義の固定ID。SkillTreeUnit の baseSkillIndex と一致させる
    public string skillName; //翻訳が無いときの名前。表示は skill.{id}.name
    public string jpSkillName; //シートの [JP]skillName。読み込み後に skillName へ移す
    public Sprite icon; //スキルアイコン。空でもよい
    public int maxLevel = 1; //上限レベル。SkillTreeUnit の段数で上書きする
    public string unit = "NA"; //表示単位。SkillBase の unit。% は100倍、Sec は秒、Count は整数。NA は数値を出さない
    public SkillParamTarget paramTarget; //効果の対象。SkillBase の paramTarget
    public int targetIndex; //SkillBase。ボールはボム0、縦レーザー1、ヘビー2、横レーザー3、オービット4、クロス5、ポイズン6
    public SkillParamStat paramStat; //効果。SkillBase の paramStat
    public float deltaValue; //SkillBase。1レベルあたりの変化量。間隔を短くする効果は負の値
}

[Serializable]
public class SkillTreeUnit
{
    public int skillTreeIndex; //配置ノードのID。段が違っても同じ値
    //public int baseSkillIndex;
    public int level; //この行が払う段。現在0なら次は1。0だけの行は全段で同じ値段
    public int maxLevel; //このスキルの上限。同じIDの行のうち最大を SkillTreeBase へ移す


    public int cost_AA;
    public int cost_BB;
    public int cost_CC;
    public int cost_DD;
    public int cost_EE;
    public int cost_FF;
    public int cost_GG;

    public int CostOf(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.AA: return cost_AA;
            case ResourceType.BB: return cost_BB;
            case ResourceType.CC: return cost_CC;
            case ResourceType.DD: return cost_DD;
            case ResourceType.EE: return cost_EE;
            case ResourceType.FF: return cost_FF;
            case ResourceType.GG: return cost_GG;
            default: return 0;
        }
    }

    public ResourceCount[] ToResourceCounts()
    {
        return new[]
        {
            new ResourceCount { resourceType = ResourceType.AA, requiredCount = cost_AA },
            new ResourceCount { resourceType = ResourceType.BB, requiredCount = cost_BB },
            new ResourceCount { resourceType = ResourceType.CC, requiredCount = cost_CC },
            new ResourceCount { resourceType = ResourceType.DD, requiredCount = cost_DD },
            new ResourceCount { resourceType = ResourceType.EE, requiredCount = cost_EE },
            new ResourceCount { resourceType = ResourceType.FF, requiredCount = cost_FF },
            new ResourceCount { resourceType = ResourceType.GG, requiredCount = cost_GG },
        };
    }

    public ResourceCount[] CopyPositiveCosts()
    {
        var counts = ToResourceCounts();
        var costs = new List<ResourceCount>();
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i].requiredCount <= 0) continue;
            costs.Add(counts[i]);
        }
        return costs.ToArray();
    }
}

[CreateAssetMenu(menuName = "SO/SkillTreeData")]
public class SO_SkillTreeData : ScriptableObject
{
    public SkillTreeBase[] skillTreeBases;
    public SkillTreeUnit[] skillTreeUnits;

    public SkillTreeUnit GetSkillTreeUnitData(int skillIndex)
    {
        if (skillTreeUnits == null) return null;
        SkillTreeUnit found = null;
        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            if (unit == null || unit.skillTreeIndex != skillIndex) continue;
            if (found == null || unit.level < found.level)
                found = unit;
        }
        return found;
    }

    public SkillTreeUnit GetSkillTreeUnitCost(int skillIndex, int level)
    {
        if (skillTreeUnits == null) return null;

        SkillTreeUnit flat = null;
        bool hasRankedCost = false;
        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            if (unit == null || unit.skillTreeIndex != skillIndex) continue;
            if (unit.level == level) return unit;
            if (unit.level > 0) hasRankedCost = true;
            else if (flat == null) flat = unit;
        }

        return hasRankedCost ? null : flat;
    }

    public SkillTreeBase GetSkillTreeBaseData(int refIndex)
    {
        if (skillTreeBases == null) return null;
        var data = Array.Find(skillTreeBases, basis => basis != null && basis.baseSkillIndex == refIndex);
        return data;
    }

    public const int GlassSkillIndex = 4020;
    public const int BallBombSkillIndex = 2000;
    public const int BallLaserSkillIndex = 2010;
    public const int BallLaserRowSkillIndex = 2020;
    public const int BallCrossSkillIndex = 2030;
    public const int BallPoisonSkillIndex = 2040;
    public const int BallHeavySkillIndex = 2050;
    public const int BallOrbitSkillIndex = 2060;
    public const int BlockCompactSkillIndex = 4010;
    public const int BallVolleySkill = 1000;
    public const int BallSpeedSkill = 1001;
    public const int BallPowerSkill = 1002;
    public const int BallHeavyHitSkill = 1003;
    public const int BallCritSkill = 1004;
    public const int BallFatalSkill = 1005;
    public const int BallAmpSkill = 1006;
    public const int BallChainSkill = 1007;
    public const int BallBossSkill = 1008;
    public const int BlockSlowSkill = 4000;
    public const int BlockHarvestSkill = 3000;
    public const int BlockMagnetSkill = 3001;
    public const int BlockLingerSkill = 3002;
    public const int RegenSpawnSkill = 3010;
    public const int RegenDropSkill = 3011;
    public const int ArmorSpawnSkill = 3020;
    public const int ArmorDropSkill = 3021;
    public const int ConvertSkill = 3030;
    public const int ConvertRateSkill = 3031;
    public const int TurretSkillIndex = 5000;
    public const int RowBeamSkillIndex = 5010;

    /// <summary>
    /// シートにガラスの行が無くても、スキル37として出現率を買えるように足す。
    /// 同じ効果の行が既にあれば何もしない。
    /// </summary>
    public void RemapLegacyIds()
    {
        if (skillTreeBases != null)
        {
            for (int i = 0; i < skillTreeBases.Length; i++)
            {
                var basis = skillTreeBases[i];
                if (basis != null && LegacySkillIds.TryGetValue(basis.baseSkillIndex, out int next))
                    basis.baseSkillIndex = next;
            }
        }

        if (skillTreeUnits != null)
        {
            for (int i = 0; i < skillTreeUnits.Length; i++)
            {
                var unit = skillTreeUnits[i];
                if (unit != null && LegacySkillIds.TryGetValue(unit.skillTreeIndex, out int next))
                    unit.skillTreeIndex = next;
            }
        }
    }

    static readonly Dictionary<int, int> LegacySkillIds = new Dictionary<int, int>
    {
        { 37, GlassSkillIndex },
        { 38, BallBombSkillIndex },
        { 39, BallLaserSkillIndex },
        { 40, BallHeavySkillIndex },
        { 41, BallLaserRowSkillIndex },
        { 42, BallOrbitSkillIndex },
        { 43, BlockCompactSkillIndex },
        { 44, BallCrossSkillIndex },
        { 45, BallPoisonSkillIndex },
        { 3000, BallVolleySkill },
        { 3001, BallSpeedSkill },
        { 3002, BallPowerSkill },
        { 3003, BallHeavyHitSkill },
        { 3004, BallCritSkill },
        { 3005, BallFatalSkill },
        { 3006, BallAmpSkill },
        { 3007, BallChainSkill },
        { 3008, BallBossSkill },
        { 3010, BlockSlowSkill },
        { 3020, BlockHarvestSkill },
        { 3021, BlockMagnetSkill },
        { 3022, BlockLingerSkill },
        { 3030, RegenSpawnSkill },
        { 3031, ArmorSpawnSkill },
        { 3032, RegenDropSkill },
        { 3033, ArmorDropSkill },
        { 3040, ConvertSkill },
        { 3041, ConvertRateSkill },
        { 3100, BallBombSkillIndex + 1 },
        { 3101, BallBombSkillIndex + 2 },
        { 3102, BallBombSkillIndex + 3 },
        { 3110, BallLaserSkillIndex + 1 },
        { 3111, BallLaserSkillIndex + 2 },
        { 3112, BallLaserSkillIndex + 3 },
        { 3120, BallLaserRowSkillIndex + 1 },
        { 3121, BallLaserRowSkillIndex + 2 },
        { 3122, BallLaserRowSkillIndex + 3 },
        { 3130, BallHeavySkillIndex + 1 },
        { 3131, BallHeavySkillIndex + 2 },
        { 3132, BallHeavySkillIndex + 3 },
        { 3140, BallOrbitSkillIndex + 1 },
        { 3141, BallOrbitSkillIndex + 2 },
        { 3142, BallOrbitSkillIndex + 3 },
        { 3150, BallCrossSkillIndex + 1 },
        { 3151, BallCrossSkillIndex + 2 },
        { 3152, BallCrossSkillIndex + 3 },
        { 3160, BallPoisonSkillIndex + 1 },
        { 3161, BallPoisonSkillIndex + 2 },
        { 3162, BallPoisonSkillIndex + 3 },
    };

    /// <summary>再取り込みで、シートにアイコン列が無いスキルのスプライトを残す。</summary>
    public void KeepIcons(SkillTreeBase[] incoming)
    {
        if (skillTreeBases == null || incoming == null) return;
        var icons = new Dictionary<int, Sprite>();
        for (int i = 0; i < skillTreeBases.Length; i++)
        {
            var basis = skillTreeBases[i];
            if (basis == null || basis.icon == null || icons.ContainsKey(basis.baseSkillIndex)) continue;
            icons[basis.baseSkillIndex] = basis.icon;
        }
        for (int i = 0; i < incoming.Length; i++)
        {
            var basis = incoming[i];
            if (basis == null || basis.icon != null) continue;
            if (icons.TryGetValue(basis.baseSkillIndex, out var icon))
                basis.icon = icon;
        }
    }

    public void ApplyImportedText()
    {
        if (skillTreeBases == null) return;
        for (int i = 0; i < skillTreeBases.Length; i++)
        {
            var basis = skillTreeBases[i];
            if (basis == null) continue;
            if (!string.IsNullOrEmpty(basis.jpSkillName))
                basis.skillName = basis.jpSkillName;
        }
    }

    public void DropOrphanUnits()
    {
        if (skillTreeUnits == null) return;
        var kept = new List<SkillTreeUnit>();
        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            if (unit != null && HasBase(unit.skillTreeIndex))
                kept.Add(unit);
        }
        skillTreeUnits = kept.ToArray();
    }

    /// <summary>同じスキルの段のうち、最大の level と maxLevel を上限にする。</summary>
    public void ApplyLimitsFromUnits()
    {
        if (skillTreeBases == null || skillTreeUnits == null) return;
        for (int i = 0; i < skillTreeBases.Length; i++)
        {
            var basis = skillTreeBases[i];
            if (basis == null) continue;
            int max = 0;
            for (int u = 0; u < skillTreeUnits.Length; u++)
            {
                var unit = skillTreeUnits[u];
                if (unit == null || unit.skillTreeIndex != basis.baseSkillIndex) continue;
                if (unit.level > max) max = unit.level;
                if (unit.maxLevel > max) max = unit.maxLevel;
            }
            if (max > 0)
                basis.maxLevel = max;
        }
    }

    /// <summary>
    /// シートに砲台が無くても、スキル5000として買えるように足す。
    /// 同じ行が既にあれば何もしない。
    /// </summary>
    public void EnsureTurretSkill()
    {
        if (!HasBase(TurretSkillIndex))
        {
            var bases = skillTreeBases != null
                ? new List<SkillTreeBase>(skillTreeBases)
                : new List<SkillTreeBase>();
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = TurretSkillIndex,
                skillName = "砲台",
                jpSkillName = "砲台",
                maxLevel = 1,
                unit = "NA",
                paramTarget = SkillParamTarget.Player,
                paramStat = SkillParamStat.TurretUnlock,
                deltaValue = 1f,
            });
            skillTreeBases = bases.ToArray();
        }

        if (skillTreeUnits != null)
        {
            for (int i = 0; i < skillTreeUnits.Length; i++)
            {
                var unit = skillTreeUnits[i];
                if (unit != null && unit.skillTreeIndex == TurretSkillIndex)
                    return;
            }
        }

        var units = skillTreeUnits != null
            ? new List<SkillTreeUnit>(skillTreeUnits)
            : new List<SkillTreeUnit>();
        units.Add(new SkillTreeUnit
        {
            skillTreeIndex = TurretSkillIndex,
            level = 1,
            maxLevel = 1,
            cost_AA = 120,
        });
        skillTreeUnits = units.ToArray();
    }

    /// <summary>
    /// シートに横ビームが無くても、スキル5010として買えるように足す。
    /// 同じ行が既にあれば何もしない。
    /// </summary>
    public void EnsureRowBeamSkill()
    {
        if (!HasBase(RowBeamSkillIndex))
        {
            var bases = skillTreeBases != null
                ? new List<SkillTreeBase>(skillTreeBases)
                : new List<SkillTreeBase>();
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = RowBeamSkillIndex,
                skillName = "横ビーム",
                jpSkillName = "横ビーム",
                maxLevel = 1,
                unit = "NA",
                paramTarget = SkillParamTarget.Player,
                paramStat = SkillParamStat.RowBeamUnlock,
                deltaValue = 1f,
            });
            skillTreeBases = bases.ToArray();
        }

        if (skillTreeUnits != null)
        {
            for (int i = 0; i < skillTreeUnits.Length; i++)
            {
                var unit = skillTreeUnits[i];
                if (unit != null && unit.skillTreeIndex == RowBeamSkillIndex)
                    return;
            }
        }

        var units = skillTreeUnits != null
            ? new List<SkillTreeUnit>(skillTreeUnits)
            : new List<SkillTreeUnit>();
        units.Add(new SkillTreeUnit
        {
            skillTreeIndex = RowBeamSkillIndex,
            level = 1,
            maxLevel = 1,
            cost_AA = 220,
        });
        skillTreeUnits = units.ToArray();
    }

    bool HasBase(int index)
    {
        if (skillTreeBases == null) return false;
        for (int i = 0; i < skillTreeBases.Length; i++)
        {
            var basis = skillTreeBases[i];
            if (basis != null && basis.baseSkillIndex == index)
                return true;
        }
        return false;
    }
}
