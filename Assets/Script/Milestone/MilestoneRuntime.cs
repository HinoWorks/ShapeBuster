using System.Collections.Generic;
using System.Numerics;
using UniRx;
using UnityEngine;
using System;



/// <summary>
/// マイルストーンの進行。SaveLoader の起動時に Bind する。
/// UI は CopyStates で一覧を取り、ProgressChanged と TierReached で更新する。
/// ボーナスは TryClaim で受け取った段階だけ効く。
/// ドロップ倍率は次の撃破から、マガジン弾数と攻撃パターンは次の InGame_Ready から効く。
/// </summary>
public static class MilestoneRuntime
{
    static readonly Subject<MilestoneProgressChange> progressChanged = new();
    static readonly Subject<MilestoneState> tierReached = new();
    static readonly List<MilestoneDef> tierUps = new();
    static CompositeDisposable subscriptions;
    static bool bound;

    public static IObservable<MilestoneProgressChange> ProgressChanged => progressChanged;
    public static IObservable<MilestoneState> TierReached => tierReached;

    public static void Bind()
    {
        if (bound) return;
        bound = true;
        subscriptions = new CompositeDisposable();

        GameEvent.InGame.DamageDealt.Subscribe(OnDamageDealt).AddTo(subscriptions);
        GameEvent.InGame.EnemyDefeated.Subscribe(OnEnemyDefeated).AddTo(subscriptions);
        GameEvent.InGame.CriticalHit.Subscribe(_ => Add(MilestoneTrack.CriticalCount, 0, BigInteger.One)).AddTo(subscriptions);
        GameEvent.InGame.ReloadStarted.Subscribe(_ => Add(MilestoneTrack.ReloadCount, 0, BigInteger.One)).AddTo(subscriptions);
        GameEvent.InGame.DamageTaken.Subscribe(OnDamageTaken).AddTo(subscriptions);
        GameEvent.UI.ResourceMod_Ingame.Subscribe(OnResource).AddTo(subscriptions);
        GameEvent.Stage.Cleared.Subscribe(_ => OnStageCleared()).AddTo(subscriptions);
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(subscriptions);
    }

    public static BigInteger GetProgress(MilestoneTrack track, int key)
    {
        return MilestoneProgress.Get(track, key);
    }

    public static bool TryGetState(int id, out MilestoneState state)
    {
        state = default;
        if (!MilestoneCatalog.TryGet(id, out var def)) return false;
        state = Build(def);
        return true;
    }

    public static void CopyStates(List<MilestoneState> dst)
    {
        if (dst == null) return;
        dst.Clear();

        var defs = MilestoneCatalog.All;
        for (int i = 0; i < defs.Count; i++)
            dst.Add(Build(defs[i]));
    }

    public static void Flush()
    {
        if (SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SaveMilestoneProgress();
    }

    /// <summary>進捗が目標に届いているとき、未受け取りを1段階受け取る。</summary>
    public static bool TryClaim(int id)
    {
        if (!MilestoneCatalog.TryGet(id, out var def)) return false;

        var state = Build(def);
        if (!state.canClaim) return false;

        MilestoneProgress.Set(MilestoneTrack.ClaimedTier, id, new BigInteger(state.claimedTier + 1));
        MilestoneBonus.Invalidate();
        progressChanged.OnNext(new MilestoneProgressChange(
            MilestoneTrack.ClaimedTier,
            id,
            new BigInteger(state.claimedTier + 1)));
        Flush();
        return true;
    }

    public static int AppliedTier(MilestoneDef def)
    {
        if (def == null) return 0;

        int maxTier = def.thresholds != null ? def.thresholds.Length : 0;
        int claimed = MilestoneProgress.GetInt(MilestoneTrack.ClaimedTier, def.id);
        int reached = MilestoneCatalog.TierOf(def, MilestoneProgress.Get(def.track, def.trackKey));
        if (claimed > reached) claimed = reached;
        if (claimed > maxTier) claimed = maxTier;
        if (claimed < 0) claimed = 0;
        return claimed;
    }

    static void OnDamageDealt((int damage, int attackIndex) info)
    {
        if (info.damage <= 0) return;

        var amount = new BigInteger(info.damage);
        Add(MilestoneTrack.TotalDamage, 0, amount);
        if (info.attackIndex >= 0)
            Add(MilestoneTrack.AttackDamage, info.attackIndex, amount);
    }

    static void OnEnemyDefeated(int enemyIndex)
    {
        if (enemyIndex >= 0)
            Add(MilestoneTrack.EnemyDefeat, enemyIndex, BigInteger.One);
        Add(MilestoneTrack.TotalDefeat, 0, BigInteger.One);
    }

    static void OnDamageTaken(int damage)
    {
        if (damage <= 0) return;
        Add(MilestoneTrack.DamageTaken, 0, new BigInteger(damage));
    }

    static void OnResource((ResourceType type, BigInteger amount) resource)
    {
        if (resource.amount <= 0) return;
        Add(MilestoneTrack.ResourceGained, (int)resource.type, resource.amount);
    }

    static void OnStageCleared()
    {
        var stage = StageTimeManager.Inst;
        if (stage == null) return;

        int seconds = Mathf.FloorToInt(stage.stageTime);
        if (seconds > 0)
            Add(MilestoneTrack.ClearTime, 0, new BigInteger(seconds));

        Add(MilestoneTrack.ClearCount, 0, BigInteger.One);
        Add(MilestoneTrack.StageClearCount, stage.activeStageIndex, BigInteger.One);
        NoteBestClear(stage.activeStageIndex, stage.stageTime);
    }

    static void OnGameState(GameStateType state)
    {
        if (state != GameStateType.InGame_End) return;

        var record = IngameRecordManager.Inst != null ? IngameRecordManager.Inst.Record : null;
        if (record != null)
            NoteBestDamage(record.damageDealt);

        Add(MilestoneTrack.PlayCount, 0, BigInteger.One);
        Flush();
    }

    static void NoteBestDamage(BigInteger damage)
    {
        if (damage <= 0) return;
        if (MilestoneProgress.Has(MilestoneTrack.BestDamage, 0) && damage <= MilestoneProgress.Get(MilestoneTrack.BestDamage, 0))
            return;

        MilestoneProgress.Set(MilestoneTrack.BestDamage, 0, damage);
        Add(MilestoneTrack.BestDamageUpdates, 0, BigInteger.One);
    }

    static void NoteBestClear(int stageIndex, float seconds)
    {
        long millis = (long)Mathf.Round(seconds * 1000f);
        if (millis <= 0) return;

        var best = new BigInteger(millis);
        if (MilestoneProgress.Has(MilestoneTrack.BestClearMillis, stageIndex)
            && best >= MilestoneProgress.Get(MilestoneTrack.BestClearMillis, stageIndex))
            return;

        MilestoneProgress.Set(MilestoneTrack.BestClearMillis, stageIndex, best);
        Add(MilestoneTrack.BestClearUpdates, 0, BigInteger.One);
    }

    static void Add(MilestoneTrack track, int key, BigInteger amount)
    {
        tierUps.Clear();
        MilestoneProgress.Add(track, key, amount, tierUps);
        progressChanged.OnNext(new MilestoneProgressChange(track, key, MilestoneProgress.Get(track, key)));

        for (int i = 0; i < tierUps.Count; i++)
            tierReached.OnNext(Build(tierUps[i]));

        if (tierUps.Count > 0)
            Flush();
    }

    static MilestoneState Build(MilestoneDef def)
    {
        var progress = MilestoneProgress.Get(def.track, def.trackKey);
        int tier = MilestoneCatalog.TierOf(def, progress);
        int claimed = AppliedTier(def);
        int maxTier = def.thresholds != null ? def.thresholds.Length : 0;
        bool complete = maxTier > 0 && claimed >= maxTier;
        bool canClaim = claimed < tier && claimed < maxTier;

        var segmentStart = BigInteger.Zero;
        var next = BigInteger.Zero;
        if (def.thresholds != null && maxTier > 0)
        {
            int goalIndex = complete ? maxTier - 1 : claimed;
            next = def.thresholds[goalIndex];
            if (goalIndex > 0)
                segmentStart = def.thresholds[goalIndex - 1];
        }

        float ratio = complete ? 1f : Ratio(progress, BigInteger.Zero, next);

        return new MilestoneState
        {
            id = def.id,
            title = def.title,
            bonusLabel = def.bonusLabel,
            track = def.track,
            trackKey = def.trackKey,
            stat = def.stat,
            bonusKey = def.bonusKey,
            apply = def.apply,
            unit = def.unit,
            bonusPerTier = def.bonusPerTier,
            currentBonus = def.bonusPerTier * claimed,
            progress = progress,
            tier = tier,
            claimedTier = claimed,
            maxTier = maxTier,
            segmentStart = segmentStart,
            nextThreshold = next,
            canClaim = canClaim,
            isComplete = complete,
            ratioToNext = ratio,
        };
    }

    static float Ratio(BigInteger progress, BigInteger start, BigInteger end)
    {
        if (end <= start) return 1f;

        var current = progress - start;
        var span = end - start;
        if (current <= 0) return 0f;
        if (current >= span) return 1f;

        double value = (double)current / (double)span;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return current > 0 ? 1f : 0f;
        return Mathf.Clamp01((float)value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        subscriptions?.Dispose();
        subscriptions = null;
        bound = false;
        tierUps.Clear();
    }
}
