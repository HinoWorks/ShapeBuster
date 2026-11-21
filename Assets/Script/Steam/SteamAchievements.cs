#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using UniRx;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam の実績。条件はゲームの進み具合（セーブ）からたしかめるので、Steam につながっていなかった回の分も、次につながったときに取れる。
/// 進み具合が変わったら印をつけ、CheckInterval 秒に 1 回まとめてたしかめる（SteamManager の Update から呼ぶ）。
/// SaveLoader が読み込みのあとに Bind する。一覧と Steamworks への登録は LevelDesign/Achievements.md。
/// id は Steamworks の「API 名」と同じにする。並びや id を変えると、取った実績と合わなくなるので、足すときは末尾に足す。
/// </summary>
public static class SteamAchievements
{
    /// <summary>条件をたしかめる間隔（秒）。</summary>
    const float CheckInterval = 1f;

    class Def
    {
        public string id;
        public string memo;
        public Func<bool> done;

        public Def(string id, string memo, Func<bool> done)
        {
            this.id = id;
            this.memo = memo;
            this.done = done;
        }
    }

    static readonly Def[] defs =
    {
        new Def("ACH_FIRST_RUN", "はじめて 1 回遊んだ", () => Track(MilestoneTrack.PlayCount) >= 1),
        new Def("ACH_FIRST_CLEAR", "ステージを初めてクリア", () => ClearedStages >= 1),
        new Def("ACH_STAGE_5", "ステージ 5 までクリア", () => ClearedStages >= 5),
        new Def("ACH_STAGE_ALL", "全ステージをクリア", () => ClearedStages >= BlockCampaign.StageCount),
        new Def("ACH_BOSS_101", "大ゾンビを倒した", () => BossDown(101)),
        new Def("ACH_BOSS_102", "甲羅ボスを倒した", () => BossDown(102)),
        new Def("ACH_BOSS_103", "再生ボスを倒した", () => BossDown(103)),
        new Def("ACH_DEFEAT_1000", "ブロックを合計 1,000 個壊した", () => Track(MilestoneTrack.TotalDefeat) >= 1000),
        new Def("ACH_DEFEAT_100000", "ブロックを合計 100,000 個壊した", () => Track(MilestoneTrack.TotalDefeat) >= 100000),
        new Def("ACH_SKILL_FIRST", "スキルを初めて覚えた", () => SkillTreeBonus.AnyLearned),
        new Def("ACH_MILESTONE_FIRST", "マイルストーンを初めて受け取った", () => AnyMilestoneClaimed()),
        new Def("ACH_BESTIARY_ALL", "図鑑の項目をすべて発見", () => Bestiary(BestiaryGoal.AllFound)),
        new Def("ACH_RESEARCH_FIRST", "図鑑の研究を初めてした", () => Bestiary(BestiaryGoal.AnyResearch)),
        new Def("ACH_RESEARCH_MAX", "どれか 1 つを最大 Lv まで研究", () => Bestiary(BestiaryGoal.AnyMax)),
        new Def("ACH_RESEARCH_ALL", "すべてを最大 Lv まで研究", () => Bestiary(BestiaryGoal.AllMax)),
        new Def("ACH_VACCINE_LAB", "ワクチン研究所を開いた", () => VaccineRuntime.Unlocked || VaccineRuntime.Pours > 0 || VaccineRuntime.Cleared),
        new Def("ACH_VACCINE_POUR", "ワクチンに初めて注いだ", () => VaccineRuntime.Pours > 0 || VaccineRuntime.Cleared),
        new Def("ACH_VACCINE_FULL", "ワクチンが完成した", () => VaccineRuntime.IsFull || VaccineRuntime.Cleared),
        new Def("ACH_VACCINE_CLEAR", "ワクチンを撒いた（エンディング）", () => VaccineRuntime.Cleared),
    };

    enum BestiaryGoal { AllFound, AnyResearch, AnyMax, AllMax }

    static readonly HashSet<string> handled = new();
    static CompositeDisposable subscriptions;
    static bool bound;
    static bool dirty;
    static float nextCheck;

    /// <summary>SaveLoader が読み込みを終えたあとに呼ぶ。</summary>
    public static void Bind()
    {
        if (bound) return;
        bound = true;
        subscriptions = new CompositeDisposable();

        BestiaryRuntime.Changed.Subscribe(_ => dirty = true).AddTo(subscriptions);
        VaccineRuntime.Changed.Subscribe(_ => dirty = true).AddTo(subscriptions);
        MilestoneRuntime.ProgressChanged.Subscribe(_ => dirty = true).AddTo(subscriptions);
        GameEvent.Stage.Cleared.Subscribe(_ => dirty = true).AddTo(subscriptions);
        GameEvent.GameState.SetGameState.Subscribe(_ => dirty = true).AddTo(subscriptions);
        GameEvent.AchieveEvent.SkillTreeUnlock.Subscribe(_ => dirty = true).AddTo(subscriptions);

        // 起動したときに、セーブにある分をまとめてたしかめる。
        dirty = true;
    }

    /// <summary>印がついていれば条件をたしかめ、満たしたものを Steam に送る。force は間隔を待たない。</summary>
    public static void Tick(bool force = false)
    {
#if !DISABLESTEAMWORKS
        if (!bound || !dirty || !SteamManager.Initialized) return;
        float now = Time.unscaledTime;
        if (!force && now < nextCheck) return;
        nextCheck = now + CheckInterval;
        dirty = false;

        List<string> unlocked = null;
        foreach (var def in defs)
        {
            if (handled.Contains(def.id) || !Met(def)) continue;

            if (!SteamUserStats.GetAchievement(def.id, out bool achieved))
            {
                // 登録されていないものは、何度たしかめても同じなので、この起動中はもう見ない。
                handled.Add(def.id);
                Debug.LogWarning($"[Steam] 実績 {def.id} が Steamworks に登録されていません。");
                continue;
            }
            if (achieved)
            {
                handled.Add(def.id);
                continue;
            }

            if (SteamUserStats.SetAchievement(def.id))
            {
                handled.Add(def.id);
                (unlocked ??= new List<string>()).Add(def.id);
                Debug.Log($"[Steam] 実績を解除しました: {def.id}（{def.memo}）");
            }
            else
            {
                // 送れなかったら、次の間隔でもう一度ためす。
                dirty = true;
                Debug.LogWarning($"[Steam] 実績 {def.id} を送れませんでした。あとでもう一度ためします。");
            }
        }

        if (unlocked != null && !SteamUserStats.StoreStats())
        {
            // 保存に失敗したら、解除した分を見直しに戻して、次の間隔でもう一度送る。
            foreach (var id in unlocked)
                handled.Remove(id);
            dirty = true;
            Debug.LogWarning("[Steam] 実績を保存できませんでした。あとでもう一度ためします。");
        }
#endif
    }

    static bool Met(Def def)
    {
        try
        {
            return def.done();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    #region -- 条件 --
    static BigInteger Track(MilestoneTrack track)
    {
        return MilestoneProgress.Get(track, 0);
    }

    /// <summary>クリアしたステージの数（最初から続けてクリアした数）。</summary>
    static int ClearedStages => SaveLoader.Inst != null ? SaveLoader.Inst.ClearedStageIndex + 1 : 0;

    static bool BossDown(int id)
    {
        return BestiaryRuntime.Get(id).bossStage >= 0;
    }

    static bool AnyMilestoneClaimed()
    {
        var all = MilestoneCatalog.All;
        if (all == null) return false;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && MilestoneProgress.GetInt(MilestoneTrack.ClaimedTier, all[i].id) >= 1)
                return true;
        }
        return false;
    }

    static bool Bestiary(BestiaryGoal goal)
    {
        var table = SOLoader.BestiaryData;
        if (table == null || table.entries == null || table.entries.Length == 0) return false;

        bool any = false;
        bool all = true;
        foreach (var data in table.entries)
        {
            if (data == null) continue;
            var state = BestiaryRuntime.Get(data.id);
            if (goal == BestiaryGoal.AllFound)
            {
                if (!state.found) return false;
                continue;
            }

            // 研究の実績は、研究できる項目（ボス以外）だけを見る。
            if (data.MaxLevel <= 0) continue;
            bool reached = goal == BestiaryGoal.AnyResearch ? state.level >= 1 : state.level >= data.MaxLevel;
            if (reached) any = true;
            else all = false;
        }

        switch (goal)
        {
            case BestiaryGoal.AllFound: return true;
            case BestiaryGoal.AllMax: return all && any;
            default: return any;
        }
    }
    #endregion

    #region -- デバッグ --
    /// <summary>実績ごとの、条件と Steam 側の状態。</summary>
    public static string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine(SteamManager.Initialized ? "Steam: つながっている" : "Steam: つながっていない");
        foreach (var def in defs)
        {
            string steam = "-";
#if !DISABLESTEAMWORKS
            if (SteamManager.Initialized)
                steam = SteamUserStats.GetAchievement(def.id, out bool achieved) ? (achieved ? "解除済み" : "未解除") : "未登録";
#endif
            sb.AppendLine($"{def.id}  条件:{(Met(def) ? "○" : "×")}  Steam:{steam}  {def.memo}");
        }
        return sb.ToString();
    }

    /// <summary>Steam 側の実績をすべて未解除に戻す（試すとき用）。条件を満たしているものは、すぐまた解除される。</summary>
    public static void DebugResetAll()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized) return;
        SteamUserStats.ResetAllStats(true);
        SteamUserStats.StoreStats();
#endif
        handled.Clear();
        dirty = true;
    }
    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        subscriptions?.Dispose();
        subscriptions = null;
        bound = false;
        dirty = false;
        nextCheck = 0f;
        handled.Clear();
    }
}
