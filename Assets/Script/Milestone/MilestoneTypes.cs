using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>マイルストーンが数える累計。</summary>
public enum MilestoneTrack
{
    EnemyDefeat = 0,
    TotalDefeat = 1,
    TotalDamage = 2,
    AttackDamage = 3,
    ClearTime = 4,
    ClearCount = 5,
    StageClearCount = 6,
    PlayCount = 7,
    ResourceGained = 8,
    CriticalCount = 9,
    ReloadCount = 10,
    DamageTaken = 11,
    BestDamage = 12,
    BestDamageUpdates = 13,
    BestClearMillis = 14,
    BestClearUpdates = 15,
    ClaimedTier = 16,
}

/// <summary>到達段階ごとに足す効果。</summary>
public enum MilestoneBonusStat
{
    EnemyDropRate = 0,
    CursorDamageRate = 1,
    CriticalRate = 2,
    AttackRate = 3,
    AttackGenerateCount = 4,
    AttackSize = 5,
    AttackDuration = 6,
    ResourceGainRate = 7,
    ResourceTypeRate = 8,
    StageDropRate = 9,
    ReloadReduction = 10,
    PlayerMaxHp = 11,
    MagazineAmmo = 12,
    AttackInterval = 13,
    CursorSize = 14,
}

/// <summary>bonusPerTier の足し方。AddPercent は 0.05 で +5%。</summary>
public enum MilestoneApply
{
    Add,
    AddPercent,
}

/// <summary>progress の単位。クリア時間は秒。</summary>
public enum MilestoneUnit
{
    Count,
    Damage,
    Seconds,
}

/// <summary>UI が固定IDで引くときの基準。個別枠は基準値 + index。</summary>
public static class MilestoneId
{
    public const int TotalDefeat = 1;
    public const int TotalDamage = 2;
    public const int ClearTime = 3;
    public const int ClearCount = 4;
    public const int PlayCount = 5;
    public const int Critical = 6;
    public const int Reload = 7;
    public const int DamageTaken = 8;
    public const int BestDamage = 9;
    public const int BestClear = 10;

    public const int EnemyDefeatBase = 10000;
    public const int AttackDamageBase = 20000;
    public const int StageClearBase = 30000;
    public const int ResourceGainedBase = 40000;
}

public sealed class MilestoneDef
{
    public int id;
    public string title;
    public string bonusLabel;
    public MilestoneTrack track;
    public int trackKey;
    public BigInteger[] thresholds;
    public MilestoneBonusStat stat;
    public int bonusKey;
    public float bonusPerTier;
    public MilestoneApply apply;
    public MilestoneUnit unit;
}

/// <summary>UI が1行を描くための現在値。</summary>
public struct MilestoneState
{
    public int id;
    public string title;
    public string bonusLabel;
    public MilestoneTrack track;
    public int trackKey;
    public MilestoneBonusStat stat;
    public int bonusKey;
    public MilestoneApply apply;
    public MilestoneUnit unit;
    public float bonusPerTier;
    public float currentBonus;
    public BigInteger progress;
    public int tier;
    public int claimedTier;
    public int maxTier;
    public BigInteger segmentStart;
    public BigInteger nextThreshold;
    public bool canClaim;
    public bool isComplete;
    public float ratioToNext;

    public string FormatPerTier()
    {
        return MilestoneText.Format(apply, bonusPerTier);
    }

    public string FormatCurrent()
    {
        return MilestoneText.Format(apply, currentBonus);
    }
}

public static class MilestoneText
{
    public static string Format(MilestoneApply apply, float value)
    {
        if (apply == MilestoneApply.AddPercent)
            return $"{value * 100f:+0.#;-0.#;0}%";

        if (Math.Abs(value - Math.Round(value)) < 0.001f)
            return $"{value:+0;-0;0}";

        return $"{value:+0.##;-0.##;0}";
    }
}

public readonly struct MilestoneProgressChange
{
    public readonly MilestoneTrack track;
    public readonly int key;
    public readonly BigInteger progress;

    public MilestoneProgressChange(MilestoneTrack track, int key, BigInteger progress)
    {
        this.track = track;
        this.key = key;
        this.progress = progress;
    }
}

[Serializable]
public class MilestoneCounterSave
{
    public int track;
    public int key;
    public string value;
}

[Serializable]
public class MilestoneProgressSave
{
    public List<MilestoneCounterSave> counters = new();
}
