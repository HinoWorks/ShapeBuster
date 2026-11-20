using System.Collections.Generic;
using System.Text;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アウトゲームのゾンビ図鑑タブ。左に雑魚とボスのカードを並べ、右に選んだ項目の詳細を出す。
/// 中身は SO_BestiaryData の並び順。進み具合は BestiaryRuntime から読む。
/// 敵の絵は Resources/Bestiary/bestiary_&lt;id&gt;（Tools → ゾンビ図鑑 → 敵の絵を書き出す で作る）。
/// 一覧の mobGrid・bossGrid にシーンで置いたカードは、編集用の仮置き。実行時にカードを作る前に消す。
/// 編集用のダミー文章は、インスペクターの右上メニュー「編集用のダミーを入れる」で入れ直せる。
/// </summary>
public class UI_BestiaryManager : MonoBehaviour
{
    [Header("一覧")]
    [SerializeField] RectTransform mobGrid;
    [SerializeField] RectTransform bossGrid;
    [SerializeField] UI_BestiaryCard cardPrefab;
    [SerializeField] TextMeshProUGUI tmp_mobTitle;
    [SerializeField] TextMeshProUGUI tmp_bossTitle;
    [SerializeField] ScrollRect scroll;

    [Header("詳細")]
    [SerializeField] Image image_portrait;
    [SerializeField] TextMeshProUGUI tmp_name;
    [SerializeField] TextMeshProUGUI tmp_level;
    [SerializeField] TextMeshProUGUI tmp_count;
    [SerializeField] TextMeshProUGUI tmp_effect;
    [SerializeField] TextMeshProUGUI tmp_diary;
    [Tooltip("観察日記のスクロール。項目を選び直したら一番上に戻す")]
    [SerializeField] ScrollRect diaryScroll;
    [SerializeField] HButton button_research;
    [SerializeField] TextMeshProUGUI tmp_research;

    const string PortraitPath = "Bestiary/bestiary_";
    // DD が変換で増えるので、研究ボタンの状態をこの間隔で見直す。
    const float RefreshInterval = 0.5f;
    // 研究ボタンの文字の色。研究できる／条件か DD が足りない／研究しきった。
    static readonly Color ResearchReady = Color.black;
    static readonly Color ResearchShort = new Color(0.75f, 0.1f, 0.1f, 1f);
    static readonly Color ResearchDone = new Color(0.35f, 0.35f, 0.35f, 1f);

    readonly List<UI_BestiaryCard> cards = new();
    static readonly Dictionary<int, Sprite> portraits = new();
    int selectedId = -1;
    bool inited;
    float refreshWait;

    public static string NameOf(int id) => Loc.Content("bestiary." + id + ".name", "#" + id);

    public static Sprite Portrait(int id)
    {
        if (!portraits.TryGetValue(id, out var sprite))
        {
            sprite = Resources.Load<Sprite>(PortraitPath + id);
            portraits[id] = sprite;
        }
        return sprite;
    }

    public void Init_Once()
    {
        if (inited) return;
        inited = true;

        BuildCards();
        if (button_research != null)
        {
            // 研究できたかどうかで音を変えるので、ボタンの押した音は鳴らさない。
            button_research.ClickSE = SEType.None;
            button_research.onClick.AddListener(OnClickResearch);
        }
        BestiaryRuntime.Changed
            .Subscribe(_ => { if (isActiveAndEnabled) RefreshAll(); })
            .AddTo(this);
        Loc.Changed += OnLanguageChanged;
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
    }

    void OnLanguageChanged(GameLanguage _)
    {
        if (isActiveAndEnabled) RefreshAll();
    }

    public void Init(bool isActive)
    {
        gameObject.SetActive(isActive);
        if (isActive)
            Enter();
    }

    void Enter()
    {
        if (!inited) Init_Once();
        if (selectedId < 0 && cards.Count > 0)
            selectedId = cards[0].Id;
        if (scroll != null)
            scroll.verticalNormalizedPosition = 1f;
        if (selectedId >= 0)
            Select(selectedId);
        else
            RefreshAll();
    }

    void Update()
    {
        refreshWait -= Time.unscaledDeltaTime;
        if (refreshWait > 0f) return;
        refreshWait = RefreshInterval;
        RefreshResearch();
        foreach (var card in cards)
            card.Refresh(card.Id == selectedId);
    }

    void BuildCards()
    {
        var table = SOLoader.BestiaryData;
        var entries = table != null ? table.entries : null;
        if (entries == null) return;

        // シーンに仮置きしたカード（編集用）は消してから作る。
        ClearPlaceholders(mobGrid);
        ClearPlaceholders(bossGrid);

        foreach (var entry in entries)
        {
            if (entry == null || cardPrefab == null) continue;
            var parent = entry.boss ? bossGrid : mobGrid;
            if (parent == null) continue;
            var card = Instantiate(cardPrefab, parent);
            card.gameObject.SetActive(true);
            card.name = "card_" + entry.id;
            card.Setup(entry.id, Select);
            cards.Add(card);
        }
    }

    static void ClearPlaceholders(RectTransform grid)
    {
        if (grid == null) return;
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            var child = grid.GetChild(i);
            if (child.GetComponent<UI_BestiaryCard>() == null) continue;
            // 同じフレームのうちにレイアウトから外すため、親から外してから消す。
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    public void Select(int id)
    {
        selectedId = id;
        // 詳細を開いたら「NEW」の印を消す。
        BestiaryRuntime.MarkSeen(id);
        RefreshAll();
        if (diaryScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            diaryScroll.verticalNormalizedPosition = 1f;
        }
    }

    void RefreshAll()
    {
        if (tmp_mobTitle != null) { Loc.UseFont(tmp_mobTitle); tmp_mobTitle.SetText(Loc.Text("bestiary.section.mob")); }
        if (tmp_bossTitle != null) { Loc.UseFont(tmp_bossTitle); tmp_bossTitle.SetText(Loc.Text("bestiary.section.boss")); }
        foreach (var card in cards)
            card.Refresh(card.Id == selectedId);
        RefreshDetail();
    }

    void RefreshDetail()
    {
        var data = BestiaryRuntime.Data(selectedId);
        var state = BestiaryRuntime.Get(selectedId);
        bool found = data != null && state.found;

        if (image_portrait != null)
        {
            image_portrait.sprite = Portrait(selectedId);
            image_portrait.enabled = image_portrait.sprite != null;
            image_portrait.preserveAspect = true;
            image_portrait.color = found ? Color.white : new Color(0f, 0f, 0f, 0.85f);
        }
        SetText(tmp_name, found ? NameOf(selectedId) : "???");

        if (!found)
        {
            SetText(tmp_level, "");
            SetText(tmp_count, "");
            SetText(tmp_effect, "");
            SetText(tmp_diary, Loc.Text("bestiary.notFound"));
            RefreshResearch();
            return;
        }

        if (data.boss)
        {
            SetText(tmp_level, Loc.Text("bestiary.boss.recordOnly"));
            SetText(tmp_count, BossRecord(state));
            SetText(tmp_effect, "");
        }
        else
        {
            SetText(tmp_level, Loc.Format("bestiary.level", state.level, data.MaxLevel));
            SetText(tmp_count, CountText(data, state));
            SetText(tmp_effect, EffectText(data, state.level));
        }
        SetText(tmp_diary, DiaryText(data, state, VaccineRuntime.Cleared));
        RefreshResearch();
    }

    void RefreshResearch()
    {
        if (button_research == null) return;
        var data = BestiaryRuntime.Data(selectedId);
        var state = BestiaryRuntime.Get(selectedId);
        bool show = data != null && !data.boss && state.found;
        button_research.gameObject.SetActive(show);
        if (!show) return;

        // 押せない状態にすると鍵の絵で値段が隠れるので、ボタンはいつも押せるままにする。
        // 研究できないときは文字の色で知らせ、押したらエラー音を鳴らす（OnClickResearch）。
        var block = BestiaryRuntime.CheckResearch(selectedId);
        if (tmp_research == null) return;
        Loc.UseFont(tmp_research);
        if (block == BestiaryResearchBlock.MaxLevel)
        {
            tmp_research.SetText(Loc.Text("bestiary.research.max"));
            tmp_research.color = ResearchDone;
        }
        else
        {
            tmp_research.SetText(Loc.Format("bestiary.research", data.Cost(state.level + 1)));
            tmp_research.color = block == BestiaryResearchBlock.None ? ResearchReady : ResearchShort;
        }
    }

    void OnClickResearch()
    {
        var data = BestiaryRuntime.Data(selectedId);
        if (!BestiaryRuntime.TryResearch(selectedId))
        {
            SoundManager.SE(SEType.UI_Error);
            return;
        }
        bool max = data != null && BestiaryRuntime.LevelOf(selectedId) >= data.MaxLevel;
        SoundManager.SE(max ? SEType.UI_PurchaseMax : SEType.UI_Purchase);
        RefreshAll();
    }

    static string CountText(BestiaryData data, BestiaryEntryState state)
    {
        string key = data.count == BestiaryCount.Hit ? "bestiary.count.hit" : "bestiary.count.kill";
        var sb = new StringBuilder(Loc.Format(key, state.count.ToString("N0")));
        if (state.level < data.MaxLevel)
        {
            sb.Append('\n');
            sb.Append(Loc.Format("bestiary.next", state.count.ToString("N0"), data.Requirement(state.level + 1).ToString("N0")));
        }
        return sb.ToString();
    }

    static string EffectText(BestiaryData data, int level)
    {
        string key = "bestiary.effect." + data.effect;
        var sb = new StringBuilder();
        sb.Append(Loc.Text("bestiary.effect.now")).Append("  ");
        sb.Append(Loc.Format(key, Percent(data.perLevel * level)));
        if (level < data.MaxLevel)
        {
            sb.Append('\n');
            sb.Append(Loc.Text("bestiary.effect.next")).Append("  ");
            sb.Append(Loc.Format(key, Percent(data.perLevel * (level + 1))));
        }
        return sb.ToString();
    }

    static string Percent(float value)
    {
        return (value * 100f).ToString("0.#");
    }

    static string BossRecord(BestiaryEntryState state)
    {
        if (state.bossStage < 0) return "";
        return Loc.Format("bestiary.boss.stage", state.bossStage) + "\n"
            + Loc.Format("bestiary.boss.time", state.bossSeconds.ToString("0.0"));
    }

    /// <summary>
    /// 観察日記。発見したときの日記に、研究 Lv の数だけ行を足していく。
    /// ワクチンを撒いたあとは、最後に「そのあと」の一行（bestiary.&lt;id&gt;.after）を足す。
    /// </summary>
    static string DiaryText(BestiaryData data, BestiaryEntryState state, bool withAfter)
    {
        var sb = new StringBuilder();
        int lines = data.boss ? 0 : Mathf.Min(state.level, data.MaxLevel);
        for (int i = 0; i <= lines; i++)
        {
            string line = Loc.Content("bestiary." + data.id + ".diary" + i, "");
            if (string.IsNullOrEmpty(line)) continue;
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(line);
        }
        if (withAfter)
        {
            string after = Loc.Content("bestiary." + data.id + ".after", "");
            if (!string.IsNullOrEmpty(after))
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("<b>").Append(Loc.Text("bestiary.afterTitle")).Append("</b>\n").Append(after);
            }
        }
        return sb.ToString();
    }

    static void SetText(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        Loc.UseFont(label);
        label.SetText(text);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 編集用。実際に出る文と同じ作りのダミーを、シーンの文字とカードに入れる（実行すると本物で上書きされる）。
    /// 詳細は、文が一番長くなる形（最初の雑魚の、最大の一つ手前の Lv。「そのあと」の一行つき）にする。
    /// </summary>
    [ContextMenu("編集用のダミーを入れる")]
    public void FillDummy()
    {
        var table = SOLoader.BestiaryData;
        var entries = table != null ? table.entries : null;
        if (entries == null) return;

        BestiaryData mob = null;
        var mobs = new List<BestiaryData>();
        var bosses = new List<BestiaryData>();
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (entry.boss) bosses.Add(entry);
            else
            {
                mobs.Add(entry);
                if (mob == null && entry.MaxLevel > 0) mob = entry;
            }
        }
        if (mob == null) return;

        int level = Mathf.Max(1, mob.MaxLevel - 1);
        long now = mob.Requirement(level);
        long next = mob.Requirement(level + 1);
        var state = new BestiaryEntryState { id = mob.id, found = true, seen = true, level = level, count = now + (next - now) / 2 };

        var labels = new Object[] { tmp_mobTitle, tmp_bossTitle, tmp_name, tmp_level, tmp_count, tmp_effect, tmp_diary, tmp_research, image_portrait };
        foreach (var label in labels)
            if (label != null) UnityEditor.Undo.RecordObject(label, "図鑑のダミー");

        if (tmp_mobTitle != null) SetText(tmp_mobTitle, Loc.Text("bestiary.section.mob"));
        if (tmp_bossTitle != null) SetText(tmp_bossTitle, Loc.Text("bestiary.section.boss"));
        if (image_portrait != null)
        {
            image_portrait.sprite = Portrait(mob.id);
            image_portrait.preserveAspect = true;
            image_portrait.color = Color.white;
        }
        SetText(tmp_name, NameOf(mob.id));
        SetText(tmp_level, Loc.Format("bestiary.level", state.level, mob.MaxLevel));
        SetText(tmp_count, CountText(mob, state));
        SetText(tmp_effect, EffectText(mob, state.level));
        SetText(tmp_diary, DiaryText(mob, state, true));
        if (tmp_research != null)
        {
            SetText(tmp_research, Loc.Format("bestiary.research", mob.Cost(state.level + 1)));
            tmp_research.color = ResearchReady;
        }
        foreach (var label in labels)
        {
            if (label == null) continue;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            UnityEditor.EditorUtility.SetDirty(label);
        }

        FillDummyCards(mobGrid, mobs);
        FillDummyCards(bossGrid, bosses);
    }

    /// <summary>仮置きのカードに、並び順どおりの敵を入れる。敵より多いカードは「???」（未発見）にする。</summary>
    static void FillDummyCards(RectTransform grid, List<BestiaryData> list)
    {
        if (grid == null) return;
        int index = 0;
        foreach (Transform child in grid)
        {
            var card = child.GetComponent<UI_BestiaryCard>();
            if (card == null) continue;
            var data = index < list.Count ? list[index] : null;
            int level = data != null && !data.boss ? Mathf.Clamp(index, 0, data.MaxLevel) : 0;
            card.EditorPreview(data, level);
            index++;
        }
    }
#endif
}
