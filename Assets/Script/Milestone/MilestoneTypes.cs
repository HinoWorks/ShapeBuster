using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>マイルストーンが数える累計。BestDamage、BestClearMillis、ClaimedTier は内部記録で、シートの行には書かない。</summary>
public enum MilestoneTrack
{
    BlockBreak = 0, //使わない。種類ごとの撃破数はゾンビ図鑑へ移した。値はアセットに残るので消さない
    TotalDefeat = 1, //壊したブロックの合計
    TotalDamage = 2, //与えたダメージの合計
    BallDamage = 3, //そのボールの与ダメージ。trackKey は BlockBallKind。直撃に爆風、レーザー、衛星、沼を含む
    ClearTime = 4, //クリア時間の合計。単位は秒
    ClearCount = 5, //クリア回数
    StageClearCount = 6, //そのステージのクリア回数。trackKey はステージ index
    PlayCount = 7, //プレイ回数。クリアした回も1プレイに含む
    ResourceGained = 8, //そのリソースの獲得数。trackKey は AA=0 … FF=5
    CriticalCount = 9, //クリティカルが出た回数
    LaunchCount = 10, //ボールを放った回数
    ReturnCount = 11, //デッドライン下へ戻った回数
    BestDamage = 12, //内部記録。自己ベストダメージの値
    BestDamageUpdates = 13, //自己ベストダメージを更新した回数。記録値そのものは BestDamage が持つ
    BestClearMillis = 14, //内部記録。ステージごとの最短クリア（ミリ秒）
    BestClearUpdates = 15, //ステージの最短クリアを更新した回数。記録値そのものは BestClearMillis が持つ
    ClaimedTier = 16, //内部記録。受け取り済み段階
}

/// <summary>
/// 到達段階ごとに足す効果。同じ stat と bonusKey は受け取り段階の分だけ合算する。
/// ドロップは敵・リソース種類・全体・いまのステージの4つを足した倍率。1.4 倍なら 1 個確定で、端数の 0.4 は 40% でさらに 1 個。
/// </summary>
public enum MilestoneBonusStat
{
    BallDamageRate = 0, //全ボールの攻撃力。bonusKey は 0
    BallKindDamage = 1, //そのボールだけの攻撃力。bonusKey は BlockBallKind
    BallCritRate = 2, //ボールのクリティカル率
    BallProc = 3, //そのボールの発生率。bonusKey は BlockBallKind。素の 10% に足す
    BallHeavySpeed = 4, //ヘビーの速さ。素の 30% に足す
    BallArea = 5, //爆風や沼の半径。bonusKey は BlockBallKind
    PoisonDuration = 6, //沼が残る秒数
    ResourceGainRate = 7, //AAのドロップ数
    ResourceTypeRate = 8, //AAのドロップ数。bonusKey は AA の 0
    StageDropRate = 9, //そのステージでのAAのドロップ数。bonusKey はステージ index
    ShotInterval = 10, //発射間隔（秒）。マイナスで短くなる。0.08 秒が下限
    BallSpeed = 11, //全ボールの速さ
    BallCount = 12, //ノーマルの本数。次のラン開始時に反映
    BallCritDamage = 13, //クリティカル倍率。素の 2 倍に足す
    PickupRange = 14, //拾得範囲
    BallBoss = 15, //ボスへの攻撃力
    BallOrbitCount = 16, //オービットの衛星の数。次のラン開始時に反映
    BlockFallSlow = 17, //旧データ用。読み込み時に全ボール攻撃力へ置き換える
}

/// <summary>bonusPerTier の足し方。</summary>
public enum MilestoneApply
{
    Add, //bonusPerTier をそのまま足す。2 なら段階ごとに +2、-0.01 なら段階ごとに -0.01 秒
    AddPercent, //割合で足す。0.05 が +5%、0.005 が +0.5%、-0.02 が -2%
}

/// <summary>進捗表示の単位。ゲージ計算には使わない。</summary>
public enum MilestoneUnit
{
    Count, //回数として表示
    Damage, //ダメージとして表示
    Seconds, //秒として表示。90 は 1:30
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
    public const int Launch = 7;
    public const int Return = 8;
    public const int BestDamage = 9;
    public const int BestClear = 10;

    public const int BlockBreakBase = 10000;
    public const int BallDamageBase = 20000;
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
