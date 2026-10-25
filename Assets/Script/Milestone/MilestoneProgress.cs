using System.Collections.Generic;
using System.Numerics;

/// <summary>マイルストーンの累計値。セーブは SaveLoader が書く。</summary>
public static class MilestoneProgress
{
    static readonly Dictionary<long, BigInteger> values = new();
    static int editVersion;
    static int savedVersion;

    public static int EditVersion => editVersion;
    public static bool IsDirty => editVersion != savedVersion;

    public static long Pack(int track, int key)
    {
        return ((long)track << 32) | (uint)key;
    }

    public static BigInteger Get(MilestoneTrack track, int key)
    {
        values.TryGetValue(Pack((int)track, key), out var value);
        return value;
    }

    public static int GetInt(MilestoneTrack track, int key)
    {
        var value = Get(track, key);
        if (value <= 0) return 0;
        if (value > int.MaxValue) return int.MaxValue;
        return (int)value;
    }

    public static bool Has(MilestoneTrack track, int key)
    {
        return values.ContainsKey(Pack((int)track, key));
    }

    public static void Set(MilestoneTrack track, int key, BigInteger value)
    {
        values[Pack((int)track, key)] = value;
        editVersion++;
    }

    /// <summary>加算し、段階が増えた定義を tierUps へ入れる。</summary>
    public static void Add(MilestoneTrack track, int key, BigInteger amount, List<MilestoneDef> tierUps)
    {
        if (amount <= 0) return;

        long id = Pack((int)track, key);
        values.TryGetValue(id, out var current);
        var next = current + amount;
        values[id] = next;
        editVersion++;

        var defs = MilestoneCatalog.FindByCounter(track, key);
        if (defs == null) return;

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (MilestoneCatalog.TierOf(def, next) > MilestoneCatalog.TierOf(def, current))
                tierUps.Add(def);
        }
    }

    public static void Replace(Dictionary<long, BigInteger> loaded)
    {
        values.Clear();
        if (loaded == null) return;

        foreach (var pair in loaded)
            values[pair.Key] = pair.Value;

        editVersion = 0;
        savedVersion = 0;
    }

    public static MilestoneProgressSave Capture()
    {
        var save = new MilestoneProgressSave();
        foreach (var pair in values)
        {
            save.counters.Add(new MilestoneCounterSave
            {
                track = (int)(pair.Key >> 32),
                key = unchecked((int)pair.Key),
                value = pair.Value.ToString(),
            });
        }
        return save;
    }

    public static void MarkSaved(int version)
    {
        savedVersion = version;
    }

    public static bool TryRead(MilestoneProgressSave save, out Dictionary<long, BigInteger> loaded)
    {
        loaded = new Dictionary<long, BigInteger>();
        if (save == null) return false;
        if (save.counters == null) return true;

        for (int i = 0; i < save.counters.Count; i++)
        {
            var counter = save.counters[i];
            if (counter == null || !BigInteger.TryParse(counter.value, out var value))
                return false;

            loaded[Pack(counter.track, counter.key)] = value;
        }
        return true;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        values.Clear();
        editVersion = 0;
        savedVersion = 0;
    }
}
