using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// テスト用の自動プレイ。進行のテンポ（段を突破するまでの実時間と回数）を測るためのもの。
/// エディタのフォルダにあるので、ビルドには入らない（BlockSession 側の入口も #if UNITY_EDITOR）。
///
/// Tools → レベルデザイン → 自動プレイ → 開始（段 3 まで）／止める。
/// Play 中のゲームを、人の代わりに次のように動かす（遊び方は単純で、人より少しうまく、迷わない）。
/// - タイトル：Start を押す（初めてならオープニングから段 0 へ）。
/// - インゲーム：ボールが手元にあれば、いちばん下の壊せるゾンビ（ボスがいればボス）の真下あたりから、少し角度をばらして撃つ。
///   落ちた資源はすぐ拾う。
/// - リザルト：少し待ってアウトゲームへ戻る。
/// - アウトゲーム：マイルストーンが開いていれば受け取れるものを全部受け取り、買えるスキルを安い順に買えるだけ買う。
///   そのあと、選べるいちばん先の段を始める。
/// 目標の段をクリアしたら止まり、1 回ごとの記録を Temp/AutoPlayLog.txt とコンソールに出す。
/// 時間は実時間（Time.realtimeSinceStartup）で測る。セーブはふつうに書くので、測る前にセーブを退避しておくこと。
/// </summary>
public static class AutoPlay
{
    const string LogPath = "Temp/AutoPlayLog.txt";
    const int DefaultTargetStage = 3;
    const float ResultWait = 1.5f;
    const float BuyInterval = 0.35f;

    static bool active;
    static int targetStage;
    static float startReal;
    static float stateEnteredReal;
    static float runStartReal;
    static float ingameTotal;
    static float nextActionReal;
    static int runCount;
    static int runsInStage;
    static int lastStage = -1;
    static GameStateType lastState = (GameStateType)(-1);
    static bool runCounted;
    static readonly StringBuilder log = new();

    [MenuItem("Tools/レベルデザイン/自動プレイ/開始（段 3 まで）")]
    static void StartToStage3() => Begin(DefaultTargetStage);

    [MenuItem("Tools/レベルデザイン/自動プレイ/止める")]
    static void StopMenu() => Stop("止めました");

    public static bool Active => active;

    public static void Begin(int stage)
    {
        targetStage = stage;
        active = true;
        startReal = -1f;
        runCount = 0;
        runsInStage = 0;
        lastStage = -1;
        ingameTotal = 0f;
        lastState = (GameStateType)(-1);
        log.Clear();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        if (!EditorApplication.isPlaying)
            EditorApplication.isPlaying = true;
        Debug.Log($"[AutoPlay] 開始：段 {stage} をクリアするまで");
    }

    static void Stop(string reason)
    {
        if (!active) return;
        active = false;
        EditorApplication.update -= Tick;
        Write($"[AutoPlay] 終了：{reason}");
    }

    static void Write(string line)
    {
        Debug.Log(line);
        log.AppendLine(line);
        try { File.WriteAllText(LogPath, log.ToString()); } catch { }
    }

    static float Now => Time.realtimeSinceStartup;

    static void Tick()
    {
        if (!active) return;
        if (!EditorApplication.isPlaying)
        {
            // Play に入る途中。入り終えたら続ける。止めたら終わる。
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Stop("Play が止まりました");
            return;
        }

        var watcher = GameWatcher.Inst;
        var save = SaveLoader.Inst;
        if (watcher == null || save == null || !GameParamManager.isInitEnd) return;
        if (startReal < 0f)
        {
            startReal = Now;
            nextActionReal = Now + 1f;
            Write($"[AutoPlay] 計測開始。クリア済みの段 {save.ClearedStageIndex}");
        }

        var state = watcher.currentGameState;
        if (state != lastState)
        {
            OnStateChanged(lastState, state);
            lastState = state;
            stateEnteredReal = Now;
        }

        if (save.ClearedStageIndex >= targetStage && !GameWatcher.IsIngameScreen(state))
        {
            Write($"[AutoPlay] 段 {targetStage} を突破。合計 {Format(Now - startReal)}、{runCount} 回、インゲーム {Format(ingameTotal)}"
                  + $"（総プレイ時間の記録：{PlayTimeRecord.Summary()}）");
            Stop("目標の段に届きました");
            return;
        }

        if (watcher.IsChangingScreen || StoryIntro.Playing) return;

        switch (state)
        {
            case GameStateType.Title:
                if (Now >= nextActionReal)
                {
                    var title = Object.FindFirstObjectByType<UI_TItleManager>();
                    if (title != null) title.OnClick_ToOutGame();
                    nextActionReal = Now + 2f;
                }
                break;
            case GameStateType.InGame:
                TickIngame();
                break;
            case GameStateType.Result:
                if (Now - stateEnteredReal >= ResultWait && Now >= nextActionReal)
                {
                    watcher.ChangeScreen(GameStateType.ResultEnd_ToOutGame);
                    nextActionReal = Now + 1f;
                }
                break;
            case GameStateType.OutGame:
                TickOutgame();
                break;
        }
    }

    static void OnStateChanged(GameStateType from, GameStateType to)
    {
        if (to == GameStateType.InGame_Ready || (to == GameStateType.InGame && !GameWatcher.IsIngameScreen(from)))
        {
            runStartReal = Now;
            runCounted = false;
        }
        if (to == GameStateType.Result && !runCounted)
        {
            runCounted = true;
            runCount++;
            var stageTime = StageTimeManager.Inst;
            int stage = stageTime != null ? stageTime.activeStageIndex : -1;
            bool cleared = stageTime != null && stageTime.isStageCleared;
            if (stage != lastStage) { runsInStage = 0; lastStage = stage; }
            runsInStage++;
            float run = Now - runStartReal;
            ingameTotal += run;
            Write($"[AutoPlay] {runCount} 回目：段 {stage} の {runsInStage} 回目 {(cleared ? "突破" : "負け")}。"
                  + $"インゲーム {run:0.0} 秒（ステージの時間 {(stageTime != null ? stageTime.stageTime : 0f):0.0} 秒）、"
                  + $"ここまで {Format(Now - startReal)}、スキル合計 Lv {TotalSkillLevel()}");
        }
        if (to == GameStateType.OutGame)
            nextActionReal = Now + 1f;
    }

    // ---- インゲーム ----
    static void TickIngame()
    {
        CollectResources();

        var session = BlockSession.Inst;
        if (session == null || !session.AutoPlayCanFire) return;

        BlockPiece target = null;
        float lowest = float.PositiveInfinity;
        var pieces = session.AutoPlayPieces;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.IsSolid || !session.IsOnCamera(piece)) continue;
            if (piece.BossIndex >= 0) { target = piece; break; }
            float y = piece.transform.position.y;
            if (y < lowest) { lowest = y; target = piece; }
        }
        if (target == null) return;

        Vector2 aim = target.transform.position;
        float pad = BlockSession.BallDiameter;
        float x = Mathf.Clamp(aim.x + Random.Range(-0.4f, 0.4f), session.AutoPlayFieldLeft + pad, session.AutoPlayFieldRight - pad);
        float y0 = session.AutoPlayDeadlineY + pad * 2f;
        // ブロックに重ならない、なるべく低いところから撃つ。
        var anchor = new Vector2(x, y0);
        int mask = 1 << BlockSession.BlockLayer;
        for (int k = 0; k < 6 && Physics2D.OverlapCircle(anchor, pad, mask) != null; k++)
            anchor.x = Mathf.Clamp(anchor.x + (k % 2 == 0 ? 1 : -1) * (k + 1) * 0.3f, session.AutoPlayFieldLeft + pad, session.AutoPlayFieldRight - pad);
        if (Physics2D.OverlapCircle(anchor, pad, mask) != null) return;

        Vector2 dir = aim - anchor;
        if (dir.y < 0.2f) dir.y = 0.2f;
        dir = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f)) * dir.normalized;
        session.AutoPlayFire(anchor, dir);
    }

    static void CollectResources()
    {
        var items = Object.FindObjectsByType<ResourceCont>(FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].gameObject.activeInHierarchy)
                items[i].Collect();
        }
    }

    // ---- アウトゲーム ----
    static void TickOutgame()
    {
        if (Now < nextActionReal) return;

        if (SkillTreeBonus.MilestoneUnlocked)
        {
            foreach (var def in MilestoneCatalog.All)
            {
                while (MilestoneRuntime.TryClaim(def.id)) { }
            }
        }

        var unit = CheapestAffordable();
        if (unit != null)
        {
            unit.OnClick_Enhance();
            nextActionReal = Now + BuyInterval;
            return;
        }

        // 買えるものが無くなったら、選べるいちばん先の段を始める。
        var save = SaveLoader.Inst;
        int stage = Mathf.Min(save.ClearedStageIndex + 1, MaxStageIndex());
        StageTimeManager.Inst.SetStageIndex(stage);
        GameWatcher.Inst.ChangeScreen(GameStateType.InGame_Ready);
        nextActionReal = Now + 2f;
    }

    static int MaxStageIndex()
    {
        int max = 0;
        var data = SOLoader.StageData;
        if (data?.stageDatas != null)
            foreach (var st in data.stageDatas)
                if (st != null) max = Mathf.Max(max, st.stageIndex);
        return max;
    }

    static UI_SkillTreeUnit CheapestAffordable()
    {
        var table = SOLoader.SkillTreeData;
        if (table == null) return null;
        UI_SkillTreeUnit best = null;
        float bestCost = float.PositiveInfinity;
        foreach (var unit in Object.FindObjectsByType<UI_SkillTreeUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (unit == null || unit.skillTree == null) continue;
            if (unit.unlockState != SkillTreeUnlockState.EnhanceReady) continue;
            if (unit.level >= Mathf.Max(1, unit.skillTree.maxLevel)) continue;
            var row = table.GetSkillTreeUnitCost(unit.skillIndex, unit.level + 1);
            if (row == null) continue;
            var costs = row.CopyPositiveCosts();
            if (!StaticManager.IsResourceEnough(costs)) continue;
            float cost = Weight(costs);
            if (cost < bestCost) { bestCost = cost; best = unit; }
        }
        return best;
    }

    /// <summary>安い順に並べるための重さ。手に入りにくい資源ほど重くする（大まかな値）。</summary>
    static float Weight(ResourceCount[] costs)
    {
        float sum = 0f;
        foreach (var c in costs)
        {
            float w = c.resourceType switch
            {
                ResourceType.AA => 1f,
                ResourceType.BB => 8f,
                ResourceType.CC => 8f,
                ResourceType.DD => 50f,
                ResourceType.EE => 20f,
                _ => 200f,
            };
            sum += c.requiredCount * w;
        }
        return sum;
    }

    static int TotalSkillLevel()
    {
        int total = 0;
        foreach (var unit in Object.FindObjectsByType<UI_SkillTreeUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (unit != null) total += unit.level;
        return total;
    }

    static string Format(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60} 分 {s % 60} 秒";
    }
}
