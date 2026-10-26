using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

/// <summary>
/// マイルストーン定義。中身は SO_MilestoneData から読む。
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

        var table = SOLoader.MilestoneData;
        var rows = table != null ? table.milestoneDatas : null;
        if (rows == null || rows.Length == 0)
        {
            Debug.LogWarning("SO_MilestoneData が空です。マイルストーンは表示されません。");
            return;
        }

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;
            Add(ToDef(row));
        }
    }

    static MilestoneDef ToDef(MilestoneData row)
    {
        return new MilestoneDef
        {
            id = row.id,
            title = row.title,
            bonusLabel = row.bonusLabel,
            track = row.track,
            trackKey = row.trackKey,
            thresholds = ToSteps(row.thresholds),
            stat = row.stat,
            bonusKey = row.bonusKey,
            bonusPerTier = row.bonusPerTier,
            apply = row.apply,
            unit = row.unit,
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

    static BigInteger[] ToSteps(long[] values)
    {
        if (values == null || values.Length == 0)
            return System.Array.Empty<BigInteger>();

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
