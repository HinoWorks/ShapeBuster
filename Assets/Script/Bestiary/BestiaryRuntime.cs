using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>図鑑 1 項目の進み具合。</summary>
public class BestiaryEntryState
{
    public int id;
    /// <summary>倒した数（石は当てた回数）。</summary>
    public long count;
    /// <summary>研究 Lv。ボスは 0 のまま。</summary>
    public int level;
    /// <summary>発見済み。</summary>
    public bool found;
    /// <summary>発見したあと、図鑑で詳細を開いた（「NEW」の印を消す）。</summary>
    public bool seen;
    /// <summary>ボスを初めて倒した段。まだなら -1。</summary>
    public int bossStage = -1;
    /// <summary>ボスを初めて倒したとき、出てから倒すまでにかかった秒数。</summary>
    public float bossSeconds;
}

/// <summary>研究できるかどうかと、その理由。</summary>
public enum BestiaryResearchBlock
{
    /// <summary>研究できる。</summary>
    None,
    /// <summary>図鑑にない項目、またはボス。</summary>
    NotResearchable,
    /// <summary>まだ発見していない。</summary>
    NotFound,
    /// <summary>最大の Lv まで研究した。</summary>
    MaxLevel,
    /// <summary>撃破数（当てた回数）が条件に届いていない。</summary>
    CountShort,
    /// <summary>DD が足りない。</summary>
    CostShort,
}

/// <summary>
/// ゾンビ図鑑の進み具合。種類ごとの撃破数はここだけが数える（マイルストーンは合計だけ）。
/// SaveLoader が起動時に読み込んで Bind する。保存は InGame_End と研究のとき、終了時に SaveLoader が書く。
/// 画面は Changed（変わった項目の id）を購読して更新する。
/// </summary>
public static class BestiaryRuntime
{
    static readonly Dictionary<int, BestiaryEntryState> states = new();
    static readonly Subject<int> changed = new();
    static CompositeDisposable subscriptions;
    static bool bound;
    static int editVersion;
    static int savedVersion;

    /// <summary>進み具合が変わった項目の id。</summary>
    public static IObservable<int> Changed => changed;
    public static int EditVersion => editVersion;
    public static bool IsDirty => editVersion != savedVersion;

    public static void Bind()
    {
        if (bound) return;
        bound = true;
        subscriptions = new CompositeDisposable();
        GameEvent.GameState.SetGameState
            .Subscribe(state => { if (state == GameStateType.InGame_End) Flush(); })
            .AddTo(subscriptions);
    }

    public static BestiaryData Data(int id)
    {
        var table = SOLoader.BestiaryData;
        return table != null ? table.Find(id) : null;
    }

    /// <summary>項目の進み具合。まだ一度も数えていなければ、空の状態を返す（登録はしない）。</summary>
    public static BestiaryEntryState Get(int id)
    {
        if (states.TryGetValue(id, out var state)) return state;
        return new BestiaryEntryState { id = id };
    }

    public static int LevelOf(int id)
    {
        return states.TryGetValue(id, out var state) ? state.level : 0;
    }

    /// <summary>発見したのに、まだ詳細を開いていない項目がある（図鑑タブの「NEW」）。</summary>
    public static bool HasUnseen
    {
        get
        {
            foreach (var state in states.Values)
                if (state.found && !state.seen) return true;
            return false;
        }
    }

    static BestiaryEntryState Edit(int id)
    {
        if (!states.TryGetValue(id, out var state))
        {
            state = new BestiaryEntryState { id = id };
            states[id] = state;
        }
        return state;
    }

    static void Touch(int id)
    {
        editVersion++;
        changed.OnNext(id);
    }

    #region -- 数える --
    /// <summary>倒した。ボスは、初めて倒した段とかかった時間も残す。</summary>
    public static void NotifyKill(BlockPiece piece)
    {
        if (piece == null) return;
        int id = piece.BestiaryId;
        var data = Data(id);
        if (data == null) return;

        var state = Edit(id);
        if (data.count == BestiaryCount.Kill)
            state.count++;
        Discover(state);
        if (data.boss && state.bossStage < 0)
        {
            state.bossStage = StageTimeManager.Inst != null ? StageTimeManager.Inst.activeStageIndex : 0;
            state.bossSeconds = piece.AliveSeconds;
        }
        Touch(id);
    }

    /// <summary>ボールが直接当たった（石）。</summary>
    public static void NotifyHit(int id)
    {
        var data = Data(id);
        if (data == null || data.count != BestiaryCount.Hit) return;

        var state = Edit(id);
        state.count++;
        Discover(state);
        Touch(id);
    }

    /// <summary>画面に出た（倒せない石は、出会ったときに発見とする）。</summary>
    public static void NotifySeen(int id)
    {
        if (Data(id) == null) return;
        var state = Edit(id);
        if (state.found) return;
        Discover(state);
        Touch(id);
    }

    static void Discover(BestiaryEntryState state)
    {
        if (state.found) return;
        state.found = true;
        state.seen = false;
    }

    /// <summary>図鑑で詳細を開いた。「NEW」の印を消す。</summary>
    public static void MarkSeen(int id)
    {
        if (!states.TryGetValue(id, out var state) || !state.found || state.seen) return;
        state.seen = true;
        Touch(id);
        Flush();
    }
    #endregion

    #region -- 研究 --
    public static BestiaryResearchBlock CheckResearch(int id)
    {
        var data = Data(id);
        if (data == null || data.boss || data.MaxLevel <= 0) return BestiaryResearchBlock.NotResearchable;
        var state = Get(id);
        if (!state.found) return BestiaryResearchBlock.NotFound;
        if (state.level >= data.MaxLevel) return BestiaryResearchBlock.MaxLevel;
        int next = state.level + 1;
        if (state.count < data.Requirement(next)) return BestiaryResearchBlock.CountShort;
        if (!HasDD(data.Cost(next))) return BestiaryResearchBlock.CostShort;
        return BestiaryResearchBlock.None;
    }

    /// <summary>次の Lv を研究する。DD を払って Lv を 1 上げる。</summary>
    public static bool TryResearch(int id)
    {
        ConvertMine.Flush();
        if (CheckResearch(id) != BestiaryResearchBlock.None) return false;

        var data = Data(id);
        var state = Edit(id);
        long cost = data.Cost(state.level + 1);
        if (cost > 0 && SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SaveResource(ResourceType.DD, -cost);
        state.level++;
        BestiaryBonus.Invalidate();
        Touch(id);
        Flush();
        return true;
    }

    static bool HasDD(long cost)
    {
        if (cost <= 0) return true;
        if (cost > int.MaxValue) return false;
        return StaticManager.IsResourceEnough(ResourceType.DD, (int)cost);
    }
    #endregion

    #region -- 保存 --
    public static void Flush()
    {
        if (SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SaveBestiary();
    }

    public static void Replace(BestiarySave save)
    {
        states.Clear();
        if (save != null && save.entries != null)
        {
            foreach (var entry in save.entries)
            {
                if (entry == null) continue;
                states[entry.id] = new BestiaryEntryState
                {
                    id = entry.id,
                    count = Math.Max(0L, entry.count),
                    level = Mathf.Max(0, entry.level),
                    found = entry.found,
                    seen = entry.seen,
                    bossStage = entry.bossStage,
                    bossSeconds = entry.bossSeconds,
                };
            }
        }
        editVersion = 0;
        savedVersion = 0;
        BestiaryBonus.Invalidate();
    }

    public static BestiarySave Capture()
    {
        var save = new BestiarySave();
        foreach (var state in states.Values)
        {
            save.entries.Add(new BestiaryEntrySave
            {
                id = state.id,
                count = state.count,
                level = state.level,
                found = state.found,
                seen = state.seen,
                bossStage = state.bossStage,
                bossSeconds = state.bossSeconds,
            });
        }
        return save;
    }

    public static void MarkSaved(int version)
    {
        savedVersion = version;
    }

    /// <summary>研究 Lv のある項目を、効果の集計に渡す。</summary>
    public static IEnumerable<BestiaryEntryState> All => states.Values;
    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        states.Clear();
        subscriptions?.Dispose();
        subscriptions = null;
        bound = false;
        editVersion = 0;
        savedVersion = 0;
    }
}

/// <summary>図鑑のセーブ。Easy Save 3 で書く。</summary>
[Serializable]
public class BestiarySave
{
    public List<BestiaryEntrySave> entries = new();
}

[Serializable]
public class BestiaryEntrySave
{
    public int id;
    public long count;
    public int level;
    public bool found;
    public bool seen;
    public int bossStage = -1;
    public float bossSeconds;
}
