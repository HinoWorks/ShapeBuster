using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillParamTarget
{
    None,
    Cursor,
    Player,
    Attack
}

public enum SkillParamStat
{
    // == Cursor ==
    Damage = 1, // ダメージへの加算。基礎は1
    AttackInterval = 2, // 攻撃間隔への加算。負の値で短くなる。下限は0.05秒
    CriticalRate = 3, // クリティカル率への加算。0.05 で +5%
    ResourceUpRate = 5, // 撃破時のドロップ倍率。銃を含む全攻撃。0.25 で +25%
    ConditionalDamage = 12, // 同じ範囲の敵1体につきダメージ倍率を足す
    CriticalDamage = 13, // クリティカル倍率への加算。素の2倍に足す
    BossDamage = 14, // ボスへのダメージ倍率
    PickupRange = 18, // 拾得半径への倍率加算。0.5 で 1.5倍
    BulletRegain = 19, // 銃の攻撃で倒した時に装弾を1戻す確率
    ReloadReduction = 20, // リロード時間の短縮率。0.1 で 10%
    MagazineCount = 21, // マガジン本数の加算
    MagazineAmmo = 22, // 1本の装弾数の加算
    MissRefund = 23, // 当たらなかった攻撃が弾を消費しない確率
    StoppedAmmoRegen = 24, // 射撃停止中の毎秒装弾回復
    ReloadGuard = 25, // リロード中の被ダメージ軽減率
    LastMagDamage = 26, // 予備マガジンが無い間のダメージ倍率

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
    public string skillName; //詳細に出す名前
    public string description; //詳細の説明。[A] は今のレベルが最大なら現在値、それ以外は次のレベル値に置き換わる
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
}
