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
    public string description; //翻訳が無いときの説明。[A] は効果量。表示は skill.{id}.desc
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

    public const int GlassSkillIndex = 37;
    public const int BallBombSkillIndex = 38;
    public const int BallLaserSkillIndex = 39;
    public const int BallHeavySkillIndex = 40;
    public const int BallLaserRowSkillIndex = 41;
    public const int BallOrbitSkillIndex = 42;
    public const int BallCrossSkillIndex = 44;
    public const int BallPoisonSkillIndex = 45;
    public const int BlockCompactSkillIndex = 43;

    /// <summary>
    /// シートにガラスの行が無くても、スキル37として出現率を買えるように足す。
    /// 同じ効果の行が既にあれば何もしない。
    /// </summary>
    public void EnsureGlassSkill()
    {
        if (HasGlassSkill()) return;

        var bases = new List<SkillTreeBase>();
        if (skillTreeBases != null)
            bases.AddRange(skillTreeBases);
        bases.Add(new SkillTreeBase
        {
            baseSkillIndex = GlassSkillIndex,
            skillName = "Glass",
            description = "Glass capsules appear on [A] of spawns. Break one, then touch the item.",
            maxLevel = 5,
            unit = "%",
            paramTarget = SkillParamTarget.Player,
            targetIndex = 0,
            paramStat = SkillParamStat.GlassSpawnRate,
            deltaValue = 0.05f
        });
        skillTreeBases = bases.ToArray();

        var units = new List<SkillTreeUnit>();
        if (skillTreeUnits != null)
            units.AddRange(skillTreeUnits);
        int[] costs = { 15, 40, 90, 180, 320 };
        for (int level = 1; level <= costs.Length; level++)
        {
            units.Add(new SkillTreeUnit
            {
                skillTreeIndex = GlassSkillIndex,
                level = level,
                cost_AA = costs[level - 1]
            });
        }
        skillTreeUnits = units.ToArray();
    }

    /// <summary>
    /// シートにボールの行が無くても、特殊ボールを買えるように足す。
    /// </summary>
    public void EnsureBallSkills()
    {
        EnsureBallSkill(BallBombSkillIndex, 0, "ボムボール", "ノーマル弾のあとに同じ方向へ飛ぶ。当たると30%で周囲を巻き込む。");
        EnsureBallSkill(BallLaserSkillIndex, 1, "レーザーボール", "ノーマル弾のあとに同じ方向へ飛ぶ。当たると30%でその列を焼く。");
        EnsureBallSkill(BallLaserRowSkillIndex, 3, "横レーザーボール", "ノーマル弾のあとに同じ方向へ飛ぶ。当たると30%でその段を焼く。");
        EnsureBallSkill(BallHeavySkillIndex, 2, "ヘビーボール", "ノーマル弾のあとに同じ方向へ飛ぶ。遅く、ブロックを貫通する。");
        EnsureBallSkill(BallOrbitSkillIndex, 4, "オービットボール", "ノーマル弾のあとに同じ方向へ飛ぶ。周りを回る玉もブロックを削る。");
        EnsureBallSkill(BallCrossSkillIndex, 5, "クロスレーザーボール", "ノーマル弾のあとに同じ方向へ飛ぶ。当たると30%で斜め2本のレーザーを出す。");
        EnsureBallSkill(BallPoisonSkillIndex, 6, "ポイズンボール", "ノーマル弾のあとに同じ方向へ飛ぶ。当たると30%で沼を残し、数秒間まわりを溶かす。");
    }

    /// <summary>
    /// シートに細片の行が無くても、ブロック縮小と破壊資源を買えるように足す。
    /// </summary>
    public void EnsureBlockCompactSkill()
    {
        if (HasBase(BlockCompactSkillIndex)) return;

        var bases = new List<SkillTreeBase>();
        if (skillTreeBases != null)
            bases.AddRange(skillTreeBases);
        bases.Add(new SkillTreeBase
        {
            baseSkillIndex = BlockCompactSkillIndex,
            skillName = "細片",
            description = "ステージの列数を基準に、ブロックを小さくして列を増やす。[A]",
            maxLevel = 5,
            unit = "NA",
            paramTarget = SkillParamTarget.Block,
            targetIndex = 0,
            paramStat = SkillParamStat.BlockCompact,
            deltaValue = 1f
        });
        skillTreeBases = bases.ToArray();

        var units = new List<SkillTreeUnit>();
        if (skillTreeUnits != null)
            units.AddRange(skillTreeUnits);
        int[] costs = { 25, 60, 120, 220, 380 };
        for (int level = 1; level <= costs.Length; level++)
        {
            units.Add(new SkillTreeUnit
            {
                skillTreeIndex = BlockCompactSkillIndex,
                level = level,
                cost_AA = costs[level - 1]
            });
        }
        skillTreeUnits = units.ToArray();
    }

    void EnsureBallSkill(int index, int targetIndex, string skillName, string description)
    {
        if (HasBase(index)) return;

        var bases = new List<SkillTreeBase>();
        if (skillTreeBases != null)
            bases.AddRange(skillTreeBases);
        bases.Add(new SkillTreeBase
        {
            baseSkillIndex = index,
            skillName = skillName,
            description = description,
            maxLevel = 1,
            unit = "NA",
            paramTarget = SkillParamTarget.Ball,
            targetIndex = targetIndex,
            paramStat = SkillParamStat.BallUnlock,
            deltaValue = 1f
        });
        skillTreeBases = bases.ToArray();

        var units = new List<SkillTreeUnit>();
        if (skillTreeUnits != null)
            units.AddRange(skillTreeUnits);
        units.Add(new SkillTreeUnit
        {
            skillTreeIndex = index,
            level = 1,
            cost_AA = 20
        });
        skillTreeUnits = units.ToArray();
    }

    public const int BallVolleySkill = 3000;
    public const int BallSpeedSkill = 3001;
    public const int BallPowerSkill = 3002;
    public const int BallHeavyHitSkill = 3003;
    public const int BallCritSkill = 3004;
    public const int BallFatalSkill = 3005;
    public const int BallAmpSkill = 3006;
    public const int BallChainSkill = 3007;
    public const int BallBossSkill = 3008;
    public const int BlockSlowSkill = 3010;
    public const int BlockHarvestSkill = 3020;
    public const int BlockMagnetSkill = 3021;
    public const int BlockLingerSkill = 3022;
    public const int RegenSpawnSkill = 3030;
    public const int ArmorSpawnSkill = 3031;
    public const int RegenDropSkill = 3032;
    public const int ArmorDropSkill = 3033;
    public const int ConvertSkill = 3040;
    public const int ConvertRateSkill = 3041;

    /// <summary>
    /// ボールツリーの強化を、シートが無くても買えるように足す。
    /// </summary>
    public void EnsureBlockTree()
    {
        AddSkill(BallVolleySkill, "連弾", "ノーマルボールが1発増える。", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallCount, 1f, 20);
        AddSkill(BallSpeedSkill, "球速", "すべてのボールが [A] 速くなる。", 3, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallSpeed, 0.15f, 15, 40, 90);
        AddSkill(BallPowerSkill, "球威", "ボールの攻撃力が [A] 増える。", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallFlatDamage, 1f, 20);
        AddSkill(BallHeavyHitSkill, "強打", "ボールの攻撃力が [A] 増える。", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallFlatDamage, 2f, 40);
        AddSkill(BallCritSkill, "クリティカル", "ボールのクリティカル率が [A] 増える。", 4, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallCritRate, 0.1f, 20, 45, 90, 160);
        AddSkill(BallFatalSkill, "致命", "クリティカルの倍率が1増える。", 1, "Count", SkillParamTarget.Ball, 0, SkillParamStat.BallCritDamage, 1f, 80);
        AddSkill(BallAmpSkill, "増幅", "ボールの攻撃力が [A] 増える。", 2, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallDamageRate, 0.25f, 50, 120);
        AddSkill(BallChainSkill, "連撃", "同じフライトで当てるたび、次のヒットが10%強くなる。上限50%。", 1, "NA", SkillParamTarget.Ball, 0, SkillParamStat.BallChain, 1f, 60);
        AddSkill(BallBossSkill, "ボス特攻", "ボスへの攻撃力が [A] 増える。", 3, "%", SkillParamTarget.Ball, 0, SkillParamStat.BallBoss, 0.1f, 30, 70, 140);

        AddBallUpgrade(0, 3100, "爆風の発生", "ボムの発生率が [A] 増える。", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(0, 3101, "爆風の威力", "ボムの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(0, 3102, "ボム＋1", "ボムがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(1, 3110, "列の発生", "縦レーザーの発生率が [A] 増える。", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(1, 3111, "列の威力", "縦レーザーの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(1, 3112, "縦レーザー＋1", "縦レーザーがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(3, 3120, "段の発生", "横レーザーの発生率が [A] 増える。", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(3, 3121, "段の威力", "横レーザーの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(3, 3122, "横レーザー＋1", "横レーザーがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(2, 3130, "ヘビーの速さ", "ヘビーの速さがノーマルの50%になる。", SkillParamStat.BallHeavySpeed, 0.2f, "NA");
        AddBallUpgrade(2, 3131, "ヘビーの威力", "ヘビーの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(2, 3132, "ヘビー＋1", "ヘビーがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(4, 3140, "衛星", "周回する玉が1つ増える。", SkillParamStat.BallOrbitCount, 1f, "Count");
        AddBallUpgrade(4, 3141, "周回の威力", "オービットの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(4, 3142, "オービット＋1", "オービットがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(5, 3150, "斜めの発生", "クロスの発生率が [A] 増える。", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(5, 3151, "斜めの威力", "クロスの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(5, 3152, "クロス＋1", "クロスがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddBallUpgrade(6, 3160, "沼の発生", "ポイズンの発生率が [A] 増える。", SkillParamStat.BallProc, 0.4f, "%");
        AddBallUpgrade(6, 3161, "沼の威力", "ポイズンの攻撃力がノーマルの100%になる。", SkillParamStat.BallRatio, 0.5f, "NA");
        AddBallUpgrade(6, 3162, "ポイズン＋1", "ポイズンがもう1発増える。", SkillParamStat.BallExtra, 1f, "Count");

        AddSkill(BlockSlowSkill, "減速", "ブロックの下降が [A] 遅くなる。", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockFallSlow, 0.1f, 20, 50, 110);
        AddSkill(BlockHarvestSkill, "収穫", "ブロックが落とすAが [A] 増える。", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockHarvest, 0.25f, 15, 40, 90);
        AddSkill(BlockMagnetSkill, "磁石", "拾得半径が [A] 広がる。", 2, "%", SkillParamTarget.Block, 0, SkillParamStat.BlockMagnet, 0.5f, 20, 50);
        AddSkill(BlockLingerSkill, "残留", "資源がデッドラインで0.4秒残る。", 1, "NA", SkillParamTarget.Block, 0, SkillParamStat.BlockLinger, 1f, 30);
        AddSkill(RegenSpawnSkill, "再生の出現", "再生ブロックの出やすさが [A] 増える。", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.RegenSpawn, 0.04f, 25, 60, 120);
        AddSkill(ArmorSpawnSkill, "壁の出現", "壁ブロックの出やすさが [A] 増える。", 3, "%", SkillParamTarget.Block, 0, SkillParamStat.ArmorSpawn, 0.04f, 25, 60, 120);
        AddSkill(RegenDropSkill, "再生の収量", "再生ブロックのBが [A] 増える。", 2, "Count", SkillParamTarget.Block, 0, SkillParamStat.RegenDrop, 1f, 40, 90);
        AddSkill(ArmorDropSkill, "壁の収量", "壁ブロックのCが [A] 増える。", 2, "Count", SkillParamTarget.Block, 0, SkillParamStat.ArmorDrop, 1f, 40, 90);
        AddSkill(ConvertSkill, "Convert", "AAをB、C、Dに換えるタブが増える。", 1, "NA", SkillParamTarget.Block, 0, SkillParamStat.ConvertUnlock, 1f, 50);
        AddSkill(ConvertRateSkill, "効率", "Convertに必要なAAが15%減る。", 3, "Count", SkillParamTarget.Block, 0, SkillParamStat.ConvertRate, 1f, 40, 90, 160);
    }

    void AddBallUpgrade(int targetIndex, int index, string skillName, string description, SkillParamStat stat, float delta, string unit)
    {
        AddSkill(index, skillName, description, 1, unit, SkillParamTarget.Ball, targetIndex, stat, delta, 30);
    }

    void AddSkill(int index, string skillName, string description, int maxLevel, string unit, SkillParamTarget target, int targetIndex, SkillParamStat stat, float delta, params int[] costs)
    {
        if (HasBase(index) || costs == null || costs.Length == 0) return;

        var bases = new List<SkillTreeBase>();
        if (skillTreeBases != null)
            bases.AddRange(skillTreeBases);
        bases.Add(new SkillTreeBase
        {
            baseSkillIndex = index,
            skillName = skillName,
            description = description,
            maxLevel = maxLevel,
            unit = unit,
            paramTarget = target,
            targetIndex = targetIndex,
            paramStat = stat,
            deltaValue = delta
        });
        skillTreeBases = bases.ToArray();

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
