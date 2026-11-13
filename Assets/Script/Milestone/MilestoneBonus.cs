using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 受け取り済み段階の合計。ドロップは次の破壊から、ボールの本数と衛星は次のラン開始時に反映する。
/// 発射間隔は 0.08 秒まで。ブロック下降はスキルと同じく 40% が下限。
/// </summary>
public static class MilestoneBonus
{
    public const float ShotIntervalFloor = 0.08f;

    static readonly Dictionary<long, float> values = new();
    static bool dirty = true;

    public static void Invalidate()
    {
        dirty = true;
    }

    public static float Get(MilestoneBonusStat stat, int key)
    {
        Rebuild();
        values.TryGetValue(Pack(stat, key), out var value);
        return value;
    }

    public static float BallDamageRate => Mathf.Max(0f, Get(MilestoneBonusStat.BallDamageRate, 0));

    public static float KindDamage(BlockBallKind kind)
    {
        return Mathf.Max(0f, Get(MilestoneBonusStat.BallKindDamage, (int)kind));
    }

    public static float BallCritRate => Mathf.Max(0f, Get(MilestoneBonusStat.BallCritRate, 0));
    public static float BallCritDamage => Mathf.Max(0f, Get(MilestoneBonusStat.BallCritDamage, 0));
    public static float BallBoss => Mathf.Max(0f, Get(MilestoneBonusStat.BallBoss, 0));
    public static float BallSpeed => Mathf.Max(0f, Get(MilestoneBonusStat.BallSpeed, 0));
    public static int BallCount => Mathf.Max(0, Mathf.RoundToInt(Get(MilestoneBonusStat.BallCount, 0)));
    public static float HeavySpeed => Mathf.Max(0f, Get(MilestoneBonusStat.BallHeavySpeed, 0));
    public static int OrbitCount => Mathf.Max(0, Mathf.RoundToInt(Get(MilestoneBonusStat.BallOrbitCount, 0)));
    public static float PoisonDuration => Mathf.Max(0f, Get(MilestoneBonusStat.PoisonDuration, 0));
    public static float PickupRange => Mathf.Max(0f, Get(MilestoneBonusStat.PickupRange, 0));

    /// <summary>下降速度への加算。段階の値が負なので、足すと遅くなる。</summary>
    public static float BlockFallSlow => Get(MilestoneBonusStat.BlockFallSlow, 0);

    public static float Proc(BlockBallKind kind)
    {
        return Mathf.Max(0f, Get(MilestoneBonusStat.BallProc, (int)kind));
    }

    public static float Area(BlockBallKind kind)
    {
        return Mathf.Max(0f, Get(MilestoneBonusStat.BallArea, (int)kind));
    }

    public static float ShotInterval
    {
        get
        {
            float add = Get(MilestoneBonusStat.ShotInterval, 0);
            return Mathf.Max(ShotIntervalFloor, SO_Player.Current.shotInterval + add);
        }
    }

    public static int ScaleDropCount(int baseCount, ResourceType resourceType, int stageIndex)
    {
        if (baseCount <= 0) return 0;

        float bonus = Get(MilestoneBonusStat.ResourceTypeRate, (int)resourceType)
            + Get(MilestoneBonusStat.ResourceGainRate, 0)
            + (stageIndex >= 0 ? Get(MilestoneBonusStat.StageDropRate, stageIndex) : 0f);
        if (bonus <= 0f) return baseCount;

        float scaled = baseCount * (1f + bonus);
        int whole = Mathf.FloorToInt(scaled);
        if (Random.value < scaled - whole)
            whole++;
        return Mathf.Max(0, whole);
    }

    static void Rebuild()
    {
        if (!dirty) return;

        values.Clear();
        var defs = MilestoneCatalog.All;
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            int tier = MilestoneRuntime.AppliedTier(def);
            if (tier <= 0) continue;

            long id = Pack(def.stat, def.bonusKey);
            values.TryGetValue(id, out var current);
            values[id] = current + def.bonusPerTier * tier;
        }
        dirty = false;
    }

    static long Pack(MilestoneBonusStat stat, int key)
    {
        return ((long)stat << 32) | (uint)key;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        values.Clear();
        dirty = true;
    }
}
