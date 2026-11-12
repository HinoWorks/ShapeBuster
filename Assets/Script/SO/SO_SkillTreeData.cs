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
    // == Cursor ==
    Damage = 1, // ダメージへの加算。Cursor は銃。Attack は targetIndex の攻撃
    AttackInterval = 2, // 攻撃間隔への加算。負の値で短くなる。下限は0.05秒
    CriticalRate = 3, // クリティカル率への加算。0.05 で +5%
    ResourceUpRate = 5, // 撃破時のドロップ倍率。銃を含む全攻撃。0.25 で +25%
    ConditionalDamage = 12, // 同じ範囲の敵1体につきダメージ倍率を足す
    CriticalDamage = 13, // クリティカル倍率への加算。素の2倍に足す
    BossDamage = 14, // ボスへのダメージ倍率
    PickupRange = 18, // 拾得半径への倍率加算。0.5 で 1.5倍
    BulletRegain = 19, // 銃の攻撃で倒した時に装弾を1戻す確率
    ReloadReduction = 20, // リロード時間の短縮率。0.1 で 10%
    MagazineCount = 21, // 未使用。予備弾は無限
    MagazineAmmo = 22, // 1本の装弾数の加算
    MissRefund = 23, // 当たらなかった攻撃が弾を消費しない確率
    StoppedAmmoRegen = 24, // 射撃停止中の毎秒装弾回復
    ReloadGuard = 25, // リロード中の被ダメージ軽減率
    LastMagDamage = 26, // 未使用。予備弾は無限
    GlassSpawnRate = 27, // 敵が1体出るときの、ガラスカプセルの出現率。0.05 で 5%

    // == Ball ==
    BallUnlock = 28, // ブロック崩しの特殊ボールを解放。targetIndex はボム0、縦レーザー1、ヘビー2、横レーザー3、オービット4、クロス5、ポイズン6

    // == Block ==
    BlockCompact = 29, // 1レベルで一辺6%縮小し、ステージ列数から列を増やす。破壊資源は1レベル+12%

    // == Ball tree ==
    BallCount = 30, // ノーマル弾の本数
    BallSpeed = 31, // 全ボールの速さへの倍率。0.15 で +15%
    BallFlatDamage = 32, // ノーマル攻撃力への加算
    BallCritRate = 33, // ボールのクリティカル率
    BallCritDamage = 34, // クリティカル倍率への加算。素の2倍に足す
    BallDamageRate = 35, // ノーマル攻撃力への倍率
    BallChain = 36, // 1以上で、同じフライトの連続ヒットが次弾を強くする
    BallBoss = 37, // ボスへの攻撃力倍率
    BallProc = 38, // 特殊弾の発生率への加算。targetIndex はボム0から
    BallRatio = 39, // 特殊弾の攻撃力がノーマルに対して増える分。0.5 で 50% が 100% になる
    BallExtra = 40, // その特殊弾の本数
    BallHeavySpeed = 41, // ヘビーの速さの割合への加算。素の30%に足す
    BallOrbitCount = 42, // オービットの周回の数
    BlockFallSlow = 43, // ブロック下降を遅くする割合
    BlockHarvest = 44, // ブロックが落とすAへの倍率
    BlockMagnet = 45, // 拾得半径への倍率
    BlockLinger = 46, // 1以上で、資源がデッドラインに残る
    RegenSpawn = 47, // 再生ブロックの生成重み。0.04 で通常群の4%
    ArmorSpawn = 48, // 壁ブロックの生成重み
    RegenDrop = 49, // 再生ブロックの固有ドロップへの加算
    ArmorDrop = 50, // 壁ブロックの固有ドロップへの加算
    ConvertUnlock = 51, // 1以上で Convert タブを出す
    ConvertRate = 52, // Convert の必要AAを減らす段

    // == Player ==
    MaxHp = 6, // 最大HPへの加算。ラン開始時に反映
    Regen = 15, // 毎秒の体力回復
    Armor = 16, // 被ダメージ軽減。0.05 で 5%。80% が上限
    SpawnRate = 17, // 毎秒の湧き数への倍率加算。0.5 で +50%

    // == Attack ==
    AttackRate = 7, // 発生率への加算。targetIndex は雷0、貫通1、範囲2、レーザー3
    GenerateRate = 8, // 範囲のダメージ間隔と、レーザーが太くなる時間
    GenerateCount = 9, // 1回の発生数への加算
    Speed = 10, // 貫通弾の速度への加算
    Duration = 11, // 範囲が残る時間と、雷の表示時間

    // == Common ==
    None = 0, // 効果なし
    Size = 4, // カーソルは当たり判定の大きさ。攻撃は貫通の当たり、範囲の半径、レーザーの太さ
}

[Serializable]
public class SkillTreeBase
{
    public int baseSkillIndex; //スキル定義の固定ID。SkillTreeUnit の baseSkillIndex と一致させる
    public string skillName; //翻訳が無いときの名前。表示は skill.{id}.name
    public string jpSkillName; //シートの [JP]skillName。読み込み後に skillName へ移す
    public Sprite icon; //スキルアイコン。空でもよい
    public int maxLevel = 1; //上限レベル。1レベル分の効果は deltaValue
    public string unit = "NA"; //表示単位。% は100倍、Sec は秒、Count は整数。NA は数値を出さない
    public SkillParamTarget paramTarget; //効果の対象。Cursor、Player、Attack
    public int targetIndex; //Attack のとき、雷0、貫通1、範囲2、レーザー3。Cursor と Player は 0
    public SkillParamStat paramStat; //効果
    public float deltaValue; //1レベルあたりの変化量。間隔を短くする効果は負の値
}

[Serializable]
public class SkillTreeUnit
{
    public int skillTreeIndex; //配置ノードのID。段が違っても同じ値
    //public int baseSkillIndex;
    public int level; //この行が払う段。現在0なら次は1。0だけの行は全段で同じ値段


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

    public void EnsureGlassSkill()
    {
        if (!HasGlassSkill())
        {
            var bases = new List<SkillTreeBase>();
            if (skillTreeBases != null)
                bases.AddRange(skillTreeBases);
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = GlassSkillIndex,
                skillName = "ガラス",
                maxLevel = 5,
                unit = "%",
                paramTarget = SkillParamTarget.Player,
                targetIndex = 0,
                paramStat = SkillParamStat.GlassSpawnRate,
                deltaValue = 0.05f
            });
            skillTreeBases = bases.ToArray();
        }

        EnsureCosts(GlassSkillIndex, 15, 40, 90, 180, 320);
    }

    /// <summary>
    /// シートにボールの行が無くても、特殊ボールを買えるように足す。
    /// </summary>
    public void EnsureBallSkills()
    {
        EnsureBallSkill(BallBombSkillIndex, 0, "ボムボール");
        EnsureBallSkill(BallLaserSkillIndex, 1, "レーザーボール");
        EnsureBallSkill(BallLaserRowSkillIndex, 3, "横レーザーボール");
        EnsureBallSkill(BallHeavySkillIndex, 2, "ヘビーボール");
        EnsureBallSkill(BallOrbitSkillIndex, 4, "オービットボール");
        EnsureBallSkill(BallCrossSkillIndex, 5, "クロスレーザーボール");
        EnsureBallSkill(BallPoisonSkillIndex, 6, "ポイズンボール");
    }

    /// <summary>
    /// シートに細片の行が無くても、ブロック縮小と破壊資源を買えるように足す。
    /// </summary>
    public void EnsureBlockCompactSkill()
    {
        if (!HasBase(BlockCompactSkillIndex))
        {
            var bases = new List<SkillTreeBase>();
            if (skillTreeBases != null)
                bases.AddRange(skillTreeBases);
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = BlockCompactSkillIndex,
                skillName = "細片",
                maxLevel = 5,
                unit = "NA",
                paramTarget = SkillParamTarget.Block,
                targetIndex = 0,
                paramStat = SkillParamStat.BlockCompact,
                deltaValue = 1f
            });
            skillTreeBases = bases.ToArray();
        }

        EnsureCosts(BlockCompactSkillIndex, 25, 60, 120, 220, 380);
    }

    void EnsureBallSkill(int index, int targetIndex, string skillName)
    {
        if (!HasBase(index))
        {
            var bases = new List<SkillTreeBase>();
            if (skillTreeBases != null)
                bases.AddRange(skillTreeBases);
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = index,
                skillName = skillName,
                maxLevel = 1,
                unit = "NA",
                paramTarget = SkillParamTarget.Ball,
                targetIndex = targetIndex,
                paramStat = SkillParamStat.BallUnlock,
                deltaValue = 1f
            });
            skillTreeBases = bases.ToArray();
        }

        EnsureCosts(index, 20);
    }

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

    /// <summary>
    /// ボールツリーの強化を、シートが無くても買えるように足す。
    /// </summary>
    public void EnsureBlockTree()
    {
        AddSkill(BallVolleySkill, "連弾", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallCount, 1f, 20);
        AddSkill(BallSpeedSkill, "球速", 3, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallSpeed, 0.15f, 15, 40, 90);
        AddSkill(BallPowerSkill, "球威", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallFlatDamage, 1f, 20);
        AddSkill(BallHeavyHitSkill, "強打", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallFlatDamage, 2f, 40);
        AddSkill(BallCritSkill, "クリティカル", 4, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallCritRate, 0.1f, 20, 45, 90, 160);
        AddSkill(BallFatalSkill, "致命", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallCritDamage, 1f, 80);
        AddSkill(BallAmpSkill, "増幅", 2, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallDamageRate, 0.25f, 50, 120);
        AddSkill(BallChainSkill, "連撃", 1, "NA", SkillParamTarget.Ball, 0, SkillParamStat.BallChain, 1f, 60);
        AddSkill(BallBossSkill, "ボス特攻", 3, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallBoss, 0.1f, 30, 70, 140);

        AddBallUpgrade(0, BallBombSkillIndex + 1, "爆風の発生", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(0, BallBombSkillIndex + 2, "爆風の威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(0, BallBombSkillIndex + 3, "ボム＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(1, BallLaserSkillIndex + 1, "列の発生", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(1, BallLaserSkillIndex + 2, "列の威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(1, BallLaserSkillIndex + 3, "縦レーザー＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(3, BallLaserRowSkillIndex + 1, "段の発生", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(3, BallLaserRowSkillIndex + 2, "段の威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(3, BallLaserRowSkillIndex + 3, "横レーザー＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(2, BallHeavySkillIndex + 1, "ヘビーの速さ", SkillParamStat.BallHeavySpeed, 0.2f, "NA");
        AddBallUpgrade(2, BallHeavySkillIndex + 2, "ヘビーの威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(2, BallHeavySkillIndex + 3, "ヘビー＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(4, BallOrbitSkillIndex + 1, "衛星", SkillParamStat.BallOrbitCount, 1f, "Count");
        AddBallUpgrade(4, BallOrbitSkillIndex + 2, "周回の威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(4, BallOrbitSkillIndex + 3, "オービット＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(5, BallCrossSkillIndex + 1, "斜めの発生", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(5, BallCrossSkillIndex + 2, "斜めの威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(5, BallCrossSkillIndex + 3, "クロス＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(6, BallPoisonSkillIndex + 1, "沼の発生", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(6, BallPoisonSkillIndex + 2, "沼の威力", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(6, BallPoisonSkillIndex + 3, "ポイズン＋1", SkillParamStat.BallExtra, 1f, "Count");

        AddSkill(BlockSlowSkill, "減速", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockFallSlow, 0.1f, 20, 50, 110);
        AddSkill(BlockHarvestSkill, "収穫", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockHarvest, 0.25f, 15, 40, 90);
        AddSkill(BlockMagnetSkill, "磁石", 2, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockMagnet, 0.5f, 20, 50);
        AddSkill(BlockLingerSkill, "残留", 1, "NA", SkillParamTarget.Block, 0, SkillParamStat.BlockLinger, 1f, 30);
        AddSkill(RegenSpawnSkill, "再生の出現", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.RegenSpawn, 0.04f, 25, 60, 120);
        AddSkill(ArmorSpawnSkill, "壁の出現", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.ArmorSpawn, 0.04f, 25, 60, 120);
        AddSkill(RegenDropSkill, "再生の収量", 2, "Count", SkillParamTarget.Block, 0, SkillParamStat.RegenDrop, 1f, 40, 90);
        AddSkill(ArmorDropSkill, "壁の収量", 2, "Count", SkillParamTarget.Block, 0, SkillParamStat.ArmorDrop, 1f, 40, 90);
        AddSkill(ConvertSkill, "Convert", 1, "NA", SkillParamTarget.Block, 0, SkillParamStat.ConvertUnlock, 1f, 50);
        AddSkill(ConvertRateSkill, "効率", 3, "Count", SkillParamTarget.Block, 0, SkillParamStat.ConvertRate, 1f, 40, 90, 160);
    }

    void AddBallUpgrade(int targetIndex, int index, string skillName, SkillParamStat stat, float delta, string unit)
    {
        AddSkill(index, skillName, 1, unit, SkillParamTarget.Ball, targetIndex, stat, delta, 30);
    }

    void AddSkill(int index, string skillName, int maxLevel, string unit, SkillParamTarget target, int targetIndex, SkillParamStat stat, float delta, params int[] costs)
    {
        if (costs == null || costs.Length == 0) return;

        if (!HasBase(index))
        {
            var bases = new List<SkillTreeBase>();
            if (skillTreeBases != null)
                bases.AddRange(skillTreeBases);
            bases.Add(new SkillTreeBase
            {
                baseSkillIndex = index,
                skillName = skillName,
                maxLevel = maxLevel,
                unit = unit,
                paramTarget = target,
                targetIndex = targetIndex,
                paramStat = stat,
                deltaValue = delta
            });
            skillTreeBases = bases.ToArray();
        }

        EnsureCosts(index, costs);
    }

    void EnsureCosts(int index, params int[] costs)
    {
        if (costs == null || costs.Length == 0 || HasUnit(index)) return;

        var units = new List<SkillTreeUnit>();
        if (skillTreeUnits != null)
            units.AddRange(skillTreeUnits);
        for (int level = 1; level <= costs.Length; level++)
        {
            units.Add(new SkillTreeUnit
            {
                skillTreeIndex = index,
                level = level,
                cost_AA = costs[level - 1]
            });
        }
        skillTreeUnits = units.ToArray();
    }

    bool HasUnit(int index)
    {
        if (skillTreeUnits == null) return false;
        for (int i = 0; i < skillTreeUnits.Length; i++)
        {
            var unit = skillTreeUnits[i];
            if (unit != null && unit.skillTreeIndex == index)
                return true;
        }
        return false;
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

    bool HasGlassSkill()
    {
        if (skillTreeBases == null) return false;
        for (int i = 0; i < skillTreeBases.Length; i++)
        {
            var basis = skillTreeBases[i];
            if (basis != null && basis.paramStat == SkillParamStat.GlassSpawnRate)
                return true;
        }
        return false;
    }
}
