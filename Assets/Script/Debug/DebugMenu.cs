using System;
using System.ComponentModel;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SRDebugger の左端トリプルタップでパネルを開き、Options に解放ボタンを出す。
/// SRDebugger がプロジェクトに無いときは何もしない。
/// </summary>
public static class DebugMenu
{
    const int MilestoneSkill = 5000;
    const int ConvertSkill = 5001;

    static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ConfigureTriggerEarly()
    {
        registered = false;
        ApplyTrigger();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureTrigger()
    {
        ApplyTrigger();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterOptions()
    {
        if (registered) return;

        ApplyTrigger();
        var service = SRDebugInstance();
        var add = service?.GetType().GetMethod("AddOptionContainer");
        if (add == null)
        {
            Debug.LogWarning("SRDebugger が見つかりません。左端トリプルタップと解放ボタンは出ません。");
            return;
        }

        add.Invoke(service, new object[] { new UnlockOptions() });
        registered = true;
        PlaceLeftEdgeTrigger().Forget();
    }

    static async UniTaskVoid PlaceLeftEdgeTrigger()
    {
        await UniTask.Yield();
        LiftDebuggerCanvases();
        if (UnityEngine.Object.FindFirstObjectByType<LeftEdgeTrigger>() != null) return;

        var root = new GameObject("DebugLeftEdge");
        UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingLayerName = "UI_Over";
        canvas.sortingOrder = 119;
        root.AddComponent<GraphicRaycaster>();

        var strip = new GameObject("Strip");
        strip.transform.SetParent(root.transform, false);
        var rect = strip.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(72f, 0f);
        rect.anchoredPosition = Vector2.zero;

        var image = strip.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        strip.AddComponent<LeftEdgeTrigger>();
    }

    static void LiftDebuggerCanvases()
    {
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (!IsDebuggerCanvas(canvas.transform)) continue;
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "UI_Over";
        }
    }

    static bool IsDebuggerCanvas(Transform transform)
    {
        while (transform != null)
        {
            if (transform.name == "SRDebugger" || transform.name == "Trigger") return true;
            transform = transform.parent;
        }
        return false;
    }

    public static void ShowPanel()
    {
        var service = SRDebugInstance();
        var show = service?.GetType().GetMethod("ShowDebugPanel", new[] { typeof(bool) });
        show?.Invoke(service, new object[] { false });
    }

    static void ApplyTrigger()
    {
        try
        {
            ApplyTriggerCore();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"SRDebugger のトリガー設定に失敗しました: {exception.Message}");
        }
    }

    static void ApplyTriggerCore()
    {
        var settingsType = FindType("SRDebugger.Settings");
        if (settingsType == null) return;

        var instance = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (instance == null) return;

        SetEnum(instance, "_triggerEnableMode", "Enabled");
        SetEnum(instance, "_triggerBehaviour", "TripleTap");
        SetEnum(instance, "_triggerPosition", "CenterLeft");
        SetEnum(instance, "_defaultTab", "Options");
        SetBool(instance, "_isEnabled", true);
        SetBool(instance, "_requireEntryCode", false);
    }

    public static bool SkipSkillCost { get; private set; }

    static int grantAmount = 100000;
    static int stageNumber;

    public static void UnlockConvert()
    {
        Unlock(ConvertSkill);
    }

    public static void UnlockMilestone()
    {
        Unlock(MilestoneSkill);
    }

    static void Unlock(int skillIndex, int level = 1)
    {
        SetSkill(skillIndex, level);
        RefreshProgress();
    }

    static void SetSkill(int skillIndex, int level)
    {
        if (SaveLoader.Inst == null) return;

        var table = SOLoader.SkillTreeData;
        var basis = table != null ? table.GetSkillTreeBaseData(skillIndex) : null;
        int applied = level;
        if (basis != null)
            applied = Mathf.Clamp(level, 0, Mathf.Max(1, basis.maxLevel));

        SaveLoader.Inst.Request_SaveSkillTreeData(skillIndex, applied);
        if (basis != null)
            SkillTreeBonus.SetLevel(skillIndex, basis, applied);
    }

    static void SetEverySkill(bool max)
    {
        var table = SOLoader.SkillTreeData;
        if (table == null || table.skillTreeBases == null) return;

        for (int i = 0; i < table.skillTreeBases.Length; i++)
        {
            var basis = table.skillTreeBases[i];
            if (basis == null) continue;
            SetSkill(basis.baseSkillIndex, max ? Mathf.Max(1, basis.maxLevel) : 0);
        }
        RefreshProgress();
    }

    static void RefreshProgress()
    {
        GameEvent.AchieveEvent.PublishSkillTreeUnlock();
        var manager = UI_OutGameManager.Inst != null ? UI_OutGameManager.Inst.UI_SkillTreeManager : null;
        if (manager != null)
            manager.RefreshUnits().Forget();
    }

    static void Grant(ResourceType type)
    {
        if (SaveLoader.Inst == null || grantAmount <= 0) return;
        SaveLoader.Inst.Request_SaveResource(type, grantAmount);
    }

    static void BreakVisible(bool bosses)
    {
        var session = BlockSession.Inst;
        if (session == null) return;

        var pieces = UnityEngine.Object.FindObjectsByType<BlockPiece>(FindObjectsSortMode.None);
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.IsSolid) continue;
            bool boss = piece.BossIndex >= 0;
            if (boss != bosses) continue;
            if (!session.IsOnCamera(piece)) continue;
            piece.Hit(int.MaxValue);
            if (bosses) return;
        }
    }

    static object SRDebugInstance()
    {
        var type = FindType("SRDebug");
        return type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name);
            if (type != null) return type;
        }
        return null;
    }

    static void SetEnum(object target, string fieldName, string enumName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || !field.FieldType.IsEnum) return;
        try
        {
            field.SetValue(target, Enum.Parse(field.FieldType, enumName));
        }
        catch (ArgumentException)
        {
        }
    }

    static void SetBool(object target, string fieldName, bool value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(target, value);
    }

    public class LeftEdgeTrigger : MonoBehaviour, IPointerClickHandler
    {
        const float ResetSeconds = 0.9f;

        float lastTap;
        int tapCount;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - lastTap > ResetSeconds)
                tapCount = 0;

            lastTap = Time.unscaledTime;
            tapCount++;
            if (tapCount < 3) return;

            tapCount = 0;
            ShowPanel();
        }
    }

    public class UnlockOptions
    {
        [Category("リソース")]
        [SRDebugger.Sort(0)]
        [SRDebugger.NumberRange(1, 100000000)]
        [SRDebugger.Increment(10000)]
        public int 付与量
        {
            get => grantAmount;
            set => grantAmount = Mathf.Max(1, value);
        }

        [Category("リソース")]
        [SRDebugger.Sort(1)]
        public void Aを足す() => Grant(ResourceType.AA);

        [Category("リソース")]
        [SRDebugger.Sort(2)]
        public void Bを足す() => Grant(ResourceType.BB);

        [Category("リソース")]
        [SRDebugger.Sort(3)]
        public void Cを足す() => Grant(ResourceType.CC);

        [Category("リソース")]
        [SRDebugger.Sort(4)]
        public void Dを足す() => Grant(ResourceType.DD);

        [Category("リソース")]
        [SRDebugger.Sort(5)]
        public void Eを足す() => Grant(ResourceType.EE);

        [Category("リソース")]
        [SRDebugger.Sort(6)]
        public void Fを足す() => Grant(ResourceType.FF);

        [Category("リソース")]
        [SRDebugger.Sort(7)]
        public void Gを足す() => Grant(ResourceType.GG);

        [Category("リソース")]
        [SRDebugger.Sort(8)]
        public void 全種類を足す()
        {
            Grant(ResourceType.AA);
            Grant(ResourceType.BB);
            Grant(ResourceType.CC);
            Grant(ResourceType.DD);
            Grant(ResourceType.EE);
            Grant(ResourceType.FF);
            Grant(ResourceType.GG);
        }

        [Category("リソース")]
        [SRDebugger.Sort(9)]
        public void 所持を0にする()
        {
            if (SaveLoader.Inst == null) return;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                var owned = SaveLoader.Inst.Get_ResourceCount(type);
                if (owned > 0)
                    SaveLoader.Inst.Request_SaveResource(type, -owned);
            }
        }

        [Category("ボール")]
        [SRDebugger.Sort(0)]
        public void ボムを解放() => Unlock(2000);

        [Category("ボール")]
        [SRDebugger.Sort(1)]
        public void 縦レーザーを解放() => Unlock(2010);

        [Category("ボール")]
        [SRDebugger.Sort(2)]
        public void 横レーザーを解放() => Unlock(2020);

        [Category("ボール")]
        [SRDebugger.Sort(3)]
        public void ヘビーを解放() => Unlock(2050);

        [Category("ボール")]
        [SRDebugger.Sort(4)]
        public void オービットを解放() => Unlock(2060);

        [Category("ボール")]
        [SRDebugger.Sort(5)]
        public void クロスを解放() => Unlock(2030);

        [Category("ボール")]
        [SRDebugger.Sort(6)]
        public void ポイズンを解放() => Unlock(2040);

        [Category("ボール")]
        [SRDebugger.Sort(7)]
        public void 全ボールを解放()
        {
            SetSkill(2000, 1);
            SetSkill(2010, 1);
            SetSkill(2020, 1);
            SetSkill(2030, 1);
            SetSkill(2040, 1);
            SetSkill(2050, 1);
            SetSkill(2060, 1);
            RefreshProgress();
        }

        [Category("ボール")]
        [SRDebugger.Sort(8)]
        public void タレットを解放() => Unlock(2100);

        [Category("ボール")]
        [SRDebugger.Sort(9)]
        public void オートビームを解放() => Unlock(2110);

        [Category("スキル")]
        [SRDebugger.Sort(0)]
        public void Convertを解放() => UnlockConvert();

        [Category("スキル")]
        [SRDebugger.Sort(1)]
        public void Milestoneを解放() => UnlockMilestone();

        [Category("スキル")]
        [SRDebugger.Sort(2)]
        public void 全スキルを最大() => SetEverySkill(true);

        [Category("スキル")]
        [SRDebugger.Sort(3)]
        public void 全スキルを0に戻す() => SetEverySkill(false);

        [Category("スキル")]
        [SRDebugger.Sort(4)]
        public bool コストなしで強化
        {
            get => SkipSkillCost;
            set => SkipSkillCost = value;
        }

        [Category("ステージ")]
        [SRDebugger.Sort(0)]
        [SRDebugger.NumberRange(0, 99)]
        public int ステージ番号
        {
            get => stageNumber;
            set => stageNumber = Mathf.Max(0, value);
        }

        [Category("ステージ")]
        [SRDebugger.Sort(1)]
        public void このステージを開始()
        {
            if (StageTimeManager.Inst == null || GameWatcher.Inst == null) return;
            StageTimeManager.Inst.SetStageIndex(stageNumber);
            GameWatcher.Inst.ChangeScreen(GameStateType.InGame_Ready);
        }

        [Category("ステージ")]
        [SRDebugger.Sort(2)]
        public void 全ステージを解放()
        {
            var data = SOLoader.StageData;
            if (data == null || data.stageDatas == null || SaveLoader.Inst == null) return;

            int max = -1;
            for (int i = 0; i < data.stageDatas.Length; i++)
            {
                var stage = data.stageDatas[i];
                if (stage != null && stage.stageIndex > max)
                    max = stage.stageIndex;
            }
            if (max >= 0)
                SaveLoader.Inst.Request_SaveClearedStage(max);
        }

        [Category("ステージ")]
        [SRDebugger.Sort(3)]
        public void 今のステージをクリア()
        {
            if (GameWatcher.Inst == null || !GameWatcher.Inst.isInGameNow) return;
            StageTimeManager.Inst?.Set_ForceBossPhase();
        }

        [Category("プレイ")]
        [SRDebugger.Sort(0)]
        [SRDebugger.NumberRange(0, 4)]
        [SRDebugger.Increment(0.5)]
        public float 速度
        {
            get => Time.timeScale;
            set
            {
                StaticManager.ResetGameTime();
                Time.timeScale = Mathf.Clamp(value, 0f, 4f);
            }
        }

        [Category("プレイ")]
        [SRDebugger.Sort(1)]
        public void 速度を戻す()
        {
            StaticManager.ResetGameTime();
        }

        [Category("プレイ")]
        [SRDebugger.Sort(2)]
        public void 画面のブロックを壊す() => BreakVisible(false);

        [Category("プレイ")]
        [SRDebugger.Sort(3)]
        public void ボスを倒す() => BreakVisible(true);

        [Category("プレイ")]
        [SRDebugger.Sort(4)]
        public void 変換速度を最大()
        {
            ConvertMine.DebugSetSpeedLevel(ConvertMine.MaxLevel);
        }

        [Category("プレイ")]
        [SRDebugger.Sort(5)]
        public void 変換を今すぐ完了()
        {
            ConvertMine.DebugFinishDeposit();
        }
    }
}
