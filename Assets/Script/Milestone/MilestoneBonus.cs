using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 受け取り済み段階の合計。ドロップは取得時、カーソルの弾数と攻撃パターンは次の Ready で反映する。
/// リロード短縮は 50% まで。攻撃間隔は呼び出し側で 0.05 秒を下限にする。
/// </summary>
public static class MilestoneBonus
{
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

    public static int ScaleCursorDamage(int damage)
    {
        float rate = Get(MilestoneBonusStat.CursorDamageRate, 0);
        if (rate == 0f) return Mathf.Max(1, damage);
        return Mathf.Max(1, Mathf.RoundToInt(damage * (1f + rate)));
    }

    public static float ReloadScale
    {
        get
        {
            float reduction = Get(MilestoneBonusStat.ReloadReduction, 0);
            return Mathf.Max(0.5f, 1f + Mathf.Min(0f, reduction));
        }
    }

    public static int PlayerMaxHp => Mathf.RoundToInt(Get(MilestoneBonusStat.PlayerMaxHp, 0));
    public static int MagazineAmmo => Mathf.RoundToInt(Get(MilestoneBonusStat.MagazineAmmo, 0));
    public static float CriticalRate => Get(MilestoneBonusStat.CriticalRate, 0);
    public static float AttackIntervalAdd => Get(MilestoneBonusStat.AttackInterval, 0);
    public static float CursorSizeAdd => Get(MilestoneBonusStat.CursorSize, 0);

    public static int ScaleDropCount(int baseCount, int enemyIndex, ResourceType resourceType, int stageIndex)
    {
        if (baseCount <= 0) return 0;

        float bonus = Get(MilestoneBonusStat.EnemyDropRate, enemyIndex)
            + Get(MilestoneBonusStat.ResourceTypeRate, (int)resourceType)
            + Get(MilestoneBonusStat.ResourceGainRate, 0)
            + (stageIndex >= 0 ? Get(MilestoneBonusStat.StageDropRate, stageIndex) : 0f)
            + Mathf.Max(0f, SkillTreeBonus.CursorResourceUpRate);
        if (bonus <= 0f) return baseCount;

        float scaled = baseCount * (1f + bonus);
        int whole = Mathf.FloorToInt(scaled);
        if (Random.value < scaled - whole)
            whole++;
        return Mathf.Max(0, whole);
    }

    public static void ApplyAttack(AttackUnitData data)
    {
        if (data == null) return;

        float rate = Get(MilestoneBonusStat.AttackRate, data.index);
        if (rate != 0f)
            data.attackRate *= 1f + rate;

        float size = Get(MilestoneBonusStat.AttackSize, data.index);
        if (size != 0f)
            data.size *= 1f + size;

        float duration = Get(MilestoneBonusStat.AttackDuration, data.index);
        if (duration != 0f)
            data.duration += duration;

        float count = Get(MilestoneBonusStat.AttackGenerateCount, data.index);
        if (count != 0f)
            data.generateCount = Mathf.Max(0, data.generateCount + Mathf.RoundToInt(count));
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
