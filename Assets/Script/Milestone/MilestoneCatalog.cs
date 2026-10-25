using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

/// <summary>
/// マイルストーン定義。敵・ステージは SO から作る。ID はセーブと UI で固定。
/// </summary>
public static class MilestoneCatalog
{
    static List<MilestoneDef> defs;
    static Dictionary<int, MilestoneDef> byId;
    static Dictionary<long, List<MilestoneDef>> byCounter;

    public static IReadOnlyList<MilestoneDef> All
    {
        get
        {
            Ensure();
            return defs;
        }
    }

    public static bool TryGet(int id, out MilestoneDef def)
    {
        Ensure();
        return byId.TryGetValue(id, out def);
    }

    public static List<MilestoneDef> FindByCounter(MilestoneTrack track, int key)
    {
        Ensure();
        byCounter.TryGetValue(MilestoneProgress.Pack((int)track, key), out var list);
        return list;
    }

    public static int TierOf(MilestoneDef def, BigInteger progress)
    {
        if (def == null || def.thresholds == null) return 0;

        int tier = 0;
        for (int i = 0; i < def.thresholds.Length; i++)
        {
            if (progress < def.thresholds[i]) break;
            tier++;
        }
        return tier;
    }

    static void Ensure()
    {
        if (defs != null) return;

        defs = new List<MilestoneDef>();
        byId = new Dictionary<int, MilestoneDef>();
        byCounter = new Dictionary<long, List<MilestoneDef>>();

        var count = Steps(10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000);
        var damage = Steps(100, 500, 2500, 10000, 50000, 250000, 1000000, 5000000, 25000000, 100000000);
        var heavyDamage = Steps(1000, 10000, 100000, 1000000, 10000000);
        var time = Steps(60, 180, 600, 1800, 3600, 10800, 36000, 108000);
        var reloads = Steps(50, 150, 400, 1000, 2500);
        var records = Steps(1, 2, 3, 5, 8, 13, 21);

        AddEnemies(count);
        Add(Def(MilestoneId.TotalDefeat, "累計破壊数", "カーソルダメージ", MilestoneTrack.TotalDefeat, 0, count,
            MilestoneBonusStat.CursorDamageRate, 0, 0.01f, MilestoneApply.AddPercent, MilestoneUnit.Count));
        Add(Def(MilestoneId.TotalDamage, "累計与ダメージ", "クリティカル率", MilestoneTrack.TotalDamage, 0, damage,
            MilestoneBonusStat.CriticalRate, 0, 0.005f, MilestoneApply.AddPercent, MilestoneUnit.Damage));
        AddAttacks(damage, heavyDamage);
        Add(Def(MilestoneId.ClearTime, "累計クリア時間", "リソース獲得", MilestoneTrack.ClearTime, 0, time,
            MilestoneBonusStat.ResourceGainRate, 0, 0.03f, MilestoneApply.AddPercent, MilestoneUnit.Seconds));
        Add(Def(MilestoneId.ClearCount, "累計クリア回数", "リロード時間", MilestoneTrack.ClearCount, 0, count,
            MilestoneBonusStat.ReloadReduction, 0, -0.02f, MilestoneApply.AddPercent, MilestoneUnit.Count));
        AddStages(count);
        Add(Def(MilestoneId.PlayCount, "累計プレイ回数", "最大HP", MilestoneTrack.PlayCount, 0, count,
            MilestoneBonusStat.PlayerMaxHp, 0, 2f, MilestoneApply.Add, MilestoneUnit.Count));
        AddResources(count);
        Add(Def(MilestoneId.Critical, "累計クリティカル", "クリティカル率", MilestoneTrack.CriticalCount, 0, count,
            MilestoneBonusStat.CriticalRate, 0, 0.005f, MilestoneApply.AddPercent, MilestoneUnit.Count));
        Add(Def(MilestoneId.Reload, "累計リロード", "マガジン弾数", MilestoneTrack.ReloadCount, 0, reloads,
            MilestoneBonusStat.MagazineAmmo, 0, 1f, MilestoneApply.Add, MilestoneUnit.Count));
        Add(Def(MilestoneId.DamageTaken, "累計被ダメージ", "最大HP", MilestoneTrack.DamageTaken, 0, damage,
            MilestoneBonusStat.PlayerMaxHp, 0, 2f, MilestoneApply.Add, MilestoneUnit.Damage));
        Add(Def(MilestoneId.BestDamage, "最高ダメージ更新", "攻撃間隔", MilestoneTrack.BestDamageUpdates, 0, records,
            MilestoneBonusStat.AttackInterval, 0, -0.01f, MilestoneApply.Add, MilestoneUnit.Count));
        Add(Def(MilestoneId.BestClear, "最短クリア更新", "カーソルサイズ", MilestoneTrack.BestClearUpdates, 0, records,
            MilestoneBonusStat.CursorSize, 0, 0.02f, MilestoneApply.Add, MilestoneUnit.Count));
    }

    static void AddEnemies(BigInteger[] count)
    {
        var table = SOLoader.EnemyData;
        var enemies = table != null ? table.enemyDatas : null;
        if (enemies == null) return;

        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            if (enemy == null) continue;

            int index = enemy.enemyIndex;
            string name = string.IsNullOrEmpty(enemy.enemyName) ? index.ToString() : enemy.enemyName;
            Add(Def(
                MilestoneId.EnemyDefeatBase + index,
                $"{name} の破壊数",
                "この敵のドロップ",
                MilestoneTrack.EnemyDefeat,
                index,
                count,
                MilestoneBonusStat.EnemyDropRate,
                index,
                0.05f,
                MilestoneApply.AddPercent,
                MilestoneUnit.Count));
        }
    }

    static void AddAttacks(BigInteger[] damage, BigInteger[] heavyDamage)
    {
        Add(Def(MilestoneId.AttackDamageBase + (int)AttackPatternType.Lightning, "雷の与ダメージ", "雷の発生率",
            MilestoneTrack.AttackDamage, (int)AttackPatternType.Lightning, damage,
            MilestoneBonusStat.AttackRate, (int)AttackPatternType.Lightning, 0.05f, MilestoneApply.AddPercent, MilestoneUnit.Damage));
        Add(Def(MilestoneId.AttackDamageBase + (int)AttackPatternType.Pierce, "貫通の与ダメージ", "貫通の弾数",
            MilestoneTrack.AttackDamage, (int)AttackPatternType.Pierce, heavyDamage,
            MilestoneBonusStat.AttackGenerateCount, (int)AttackPatternType.Pierce, 1f, MilestoneApply.Add, MilestoneUnit.Damage));
        Add(Def(MilestoneId.AttackDamageBase + (int)AttackPatternType.AreaCircle, "範囲の与ダメージ", "範囲のサイズ",
            MilestoneTrack.AttackDamage, (int)AttackPatternType.AreaCircle, damage,
            MilestoneBonusStat.AttackSize, (int)AttackPatternType.AreaCircle, 0.05f, MilestoneApply.AddPercent, MilestoneUnit.Damage));
        Add(Def(MilestoneId.AttackDamageBase + (int)AttackPatternType.Laser, "レーザーの与ダメージ", "レーザーの持続",
            MilestoneTrack.AttackDamage, (int)AttackPatternType.Laser, damage,
            MilestoneBonusStat.AttackDuration, (int)AttackPatternType.Laser, 0.05f, MilestoneApply.Add, MilestoneUnit.Damage));
    }

    static void AddStages(BigInteger[] count)
    {
        var table = SOLoader.StageData;
        var stages = table != null ? table.stageDatas : null;
        if (stages == null) return;

        var seen = new HashSet<int>();
        for (int i = 0; i < stages.Length; i++)
        {
            var stage = stages[i];
            if (stage == null || !seen.Add(stage.stageIndex)) continue;

            Add(Def(
                MilestoneId.StageClearBase + stage.stageIndex,
                $"ステージ {stage.stageIndex} のクリア回数",
                "このステージのドロップ",
                MilestoneTrack.StageClearCount,
                stage.stageIndex,
                count,
                MilestoneBonusStat.StageDropRate,
                stage.stageIndex,
                0.05f,
                MilestoneApply.AddPercent,
                MilestoneUnit.Count));
        }
    }

    static void AddResources(BigInteger[] count)
    {
        var types = (ResourceType[])System.Enum.GetValues(typeof(ResourceType));
        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            Add(Def(
                MilestoneId.ResourceGainedBase + (int)type,
                $"{type} の累計獲得",
                "このリソースの獲得",
                MilestoneTrack.ResourceGained,
                (int)type,
                count,
                MilestoneBonusStat.ResourceTypeRate,
                (int)type,
                0.02f,
                MilestoneApply.AddPercent,
                MilestoneUnit.Count));
        }
    }

    static MilestoneDef Def(
        int id,
        string title,
        string bonusLabel,
        MilestoneTrack track,
        int trackKey,
        BigInteger[] thresholds,
        MilestoneBonusStat stat,
        int bonusKey,
        float bonusPerTier,
        MilestoneApply apply,
        MilestoneUnit unit)
    {
        return new MilestoneDef
        {
            id = id,
            title = title,
            bonusLabel = bonusLabel,
            track = track,
            trackKey = trackKey,
            thresholds = thresholds,
            stat = stat,
            bonusKey = bonusKey,
            bonusPerTier = bonusPerTier,
            apply = apply,
            unit = unit,
        };
    }

    static void Add(MilestoneDef def)
    {
        if (def == null) return;
        if (byId.ContainsKey(def.id))
        {
            Debug.LogError($"マイルストーンIDが重複しています。 id: {def.id}");
            return;
        }

        defs.Add(def);
        byId.Add(def.id, def);

        long counter = MilestoneProgress.Pack((int)def.track, def.trackKey);
        if (!byCounter.TryGetValue(counter, out var list))
        {
            list = new List<MilestoneDef>();
            byCounter.Add(counter, list);
        }
        list.Add(def);
    }

    static BigInteger[] Steps(params long[] values)
    {
        var steps = new BigInteger[values.Length];
        for (int i = 0; i < values.Length; i++)
            steps[i] = values[i];
        return steps;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        defs = null;
        byId = null;
        byCounter = null;
    }
}
