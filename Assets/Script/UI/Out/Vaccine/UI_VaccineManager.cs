using System.Text;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// アウトゲームのワクチンタブ。ゲージ・注ぐボタン・工程の記録（研究の成果と日記）を出す。撒いたことは、記録の一番上の「そのあと」と、空になったフラスコ（UI_VaccineFlask）で見せる。
/// 注ぐボタンは連打する前提で、押すたびに VaccineRuntime.TryPour を呼ぶ。
/// 満タンになったら「ワクチンを撒く」を出し、押すと撒く演出のあとタイトルへ戻る（VaccineEnding）。撒けるのは一度だけで、撒いたあとは注ぐボタンも撒くボタンも出さない。
/// 編集用のダミー文章は、インスペクターの右上メニュー「編集用のダミーを入れる」で入れ直せる。
/// </summary>
public class UI_VaccineManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_title;
    [Tooltip("ゲージの中身。Image の Fill Amount で進み具合を出す")]
    [SerializeField] Image image_gaugeFill;
    [Tooltip("完成度の割合（例：80%）")]
    [FormerlySerializedAs("tmp_progress")]
    [SerializeField] TextMeshProUGUI tmp_percent;
    [Tooltip("注いだ DD と、完成に要る DD（例：800 / 1000）")]
    [SerializeField] TextMeshProUGUI tmp_amount;
    [SerializeField] HButton button_pour;
    [SerializeField] TextMeshProUGUI tmp_pour;
    [SerializeField] HButton button_spread;
    [SerializeField] TextMeshProUGUI tmp_spread;
    [Tooltip("工程の記録。新しい工程が終わったら一番下まで送る")]
    [SerializeField] TextMeshProUGUI tmp_log;
    [SerializeField] ScrollRect logScroll;

    // DD が変換で増えるので、注ぐボタンの状態をこの間隔で見直す。
    const float RefreshInterval = 0.5f;
    static readonly Color PourReady = Color.black;
    static readonly Color PourShort = new Color(0.75f, 0.1f, 0.1f, 1f);
    static readonly Color PourDone = new Color(0.35f, 0.35f, 0.35f, 1f);

    bool inited;
    float refreshWait;
    int shownStages = -1;

    public void Init_Once()
    {
        if (inited) return;
        inited = true;

        if (button_pour != null)
        {
            // 注げたかどうかで音を変えるので、ボタンの押した音は鳴らさない。
            button_pour.ClickSE = SEType.None;
            button_pour.onClick.AddListener(OnClickPour);
        }
        if (button_spread != null)
            button_spread.onClick.AddListener(OnClickSpread);

        VaccineRuntime.Changed
            .Subscribe(_ => { if (isActiveAndEnabled) Refresh(); })
            .AddTo(this);
        Loc.Changed += OnLanguageChanged;
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
    }

    void OnLanguageChanged(GameLanguage _)
    {
        if (isActiveAndEnabled)
        {
            shownStages = -1;
            Refresh();
        }
    }

    public void Init(bool isActive)
    {
        gameObject.SetActive(isActive);
        if (!isActive) return;
        if (!inited) Init_Once();
        shownStages = -1;
        Refresh();
    }

    void Update()
    {
        refreshWait -= Time.unscaledDeltaTime;
        if (refreshWait > 0f) return;
        refreshWait = RefreshInterval;
        RefreshPour();
    }

    void Refresh()
    {
        SetText(tmp_title, Loc.Text("vaccine.title"));
        if (image_gaugeFill != null)
            image_gaugeFill.fillAmount = VaccineRuntime.Progress;
        SetProgressTexts(VaccineRuntime.Progress, VaccineRuntime.Pours * VaccineRuntime.PourCost);
        RefreshPour();

        // 撒くボタンは、満タンで、まだ撒いていないときだけ出す。撒くのは一度だけ。
        bool canSpread = VaccineRuntime.IsFull && !VaccineRuntime.Cleared;
        if (button_spread != null)
            button_spread.gameObject.SetActive(canSpread);
        SetText(tmp_spread, Loc.Text("vaccine.spread"));

        RefreshLog();
    }

    void SetProgressTexts(float progress, long poured)
    {
        if (tmp_percent != null) tmp_percent.SetText(Mathf.RoundToInt(progress * 100f) + "%");
        if (tmp_amount != null) tmp_amount.SetText(poured.ToString("N0") + " / " + VaccineRuntime.TotalCost.ToString("N0"));
    }

    void RefreshPour()
    {
        if (button_pour == null) return;
        bool full = VaccineRuntime.IsFull;
        button_pour.gameObject.SetActive(!full);
        if (full || tmp_pour == null) return;

        // 満タンのとき（撒く前・撒いたあと）は出さない。
        // 押せない状態にすると鍵の絵で値段が隠れるので、ボタンはいつも押せるままにする。
        Loc.UseFont(tmp_pour);
        tmp_pour.SetText(Loc.Format("vaccine.pour", VaccineRuntime.PourCost.ToString("N0")));
        tmp_pour.color = VaccineRuntime.CanPour() ? PourReady : PourShort;
    }

    void RefreshLog()
    {
        int done = VaccineRuntime.StagesDone;
        bool cleared = VaccineRuntime.Cleared;
        int key = done + (cleared ? 100 : 0);
        if (key == shownStages) return;
        bool grew = shownStages >= 0 && key > shownStages;
        // 撒いたあとは「そのあと」が一番上にあり、工程はもう増えないので、いつも上から読む。
        if (cleared) grew = false;
        shownStages = key;

        SetText(tmp_log, LogText(done, cleared));
        if (logScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            // 新しい工程が増えたら一番下へ、開き直したときと撒いたあとは一番上から読む。
            logScroll.verticalNormalizedPosition = grew ? 0f : 1f;
        }
    }

    // 「そのあと」の見出しの色（ワクチンのしずくと同じ緑）。
    const string AfterColor = "#8FE8A8";

    /// <summary>
    /// 工程の記録。終わった工程ごとに、名前・研究の成果・日記を並べる。
    /// 撒いたあとは、「そのあと」を一番上に出す（開いてすぐ目に入るように）。工程はその下に続く。
    /// </summary>
    static string LogText(int done, bool cleared)
    {
        var sb = new StringBuilder();
        if (cleared)
        {
            sb.Append("<b><color=").Append(AfterColor).Append('>').Append(Loc.Text("vaccine.clear.name")).Append("</color></b>\n");
            sb.Append(Loc.Text("vaccine.clear.diary"));
            sb.Append("\n\n\n");
        }
        if (done <= 0)
        {
            sb.Append(Loc.Text("vaccine.stage0"));
            return sb.ToString();
        }
        bool first = true;
        for (int i = 1; i <= done; i++)
        {
            if (!first) sb.Append("\n\n");
            first = false;
            sb.Append("<b>").Append(Loc.Text("vaccine.stage" + i + ".name")).Append("</b>\n");
            sb.Append(Loc.Text("vaccine.stage" + i + ".result")).Append('\n');
            sb.Append(Loc.Text("vaccine.stage" + i + ".diary"));
        }
        return sb.ToString();
    }

    void OnClickPour()
    {
        if (!VaccineRuntime.TryPour())
        {
            SoundManager.SE(SEType.UI_Error);
            return;
        }
        SoundManager.SE(VaccineRuntime.IsFull ? SEType.UI_PurchaseMax : SEType.UI_Purchase);
        Refresh();
    }

    void OnClickSpread()
    {
        if (!VaccineEnding.Play())
            SoundManager.SE(SEType.UI_Error);
    }

    static void SetText(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        Loc.UseFont(label);
        label.SetText(text);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 編集用。実際に出る文と同じ作りのダミーを、シーンの文字とゲージに入れる（実行すると本物で上書きされる）。
    /// 実際には同時に出ないもの（注ぐボタンと撒くボタン、記録の途中とクリア）もまとめて入れる。
    /// ゲージは途中（80%）、記録は一番長くなる形（5 工程すべてとクリアの日記）にする。
    /// 表示・非表示は切り替えない。
    /// </summary>
    [ContextMenu("編集用のダミーを入れる")]
    public void FillDummy()
    {
        const float gauge = 0.8f;
        var targets = new Object[] { tmp_title, image_gaugeFill, tmp_percent, tmp_amount, tmp_pour, tmp_spread, tmp_log };
        foreach (var target in targets)
            if (target != null) UnityEditor.Undo.RecordObject(target, "ワクチンのダミー");

        SetText(tmp_title, Loc.Text("vaccine.title"));
        if (image_gaugeFill != null) image_gaugeFill.fillAmount = gauge;
        SetProgressTexts(gauge, (long)(VaccineRuntime.TotalCost * gauge));
        if (tmp_pour != null)
        {
            SetText(tmp_pour, Loc.Format("vaccine.pour", VaccineRuntime.PourCost.ToString("N0")));
            tmp_pour.color = PourReady;
        }
        SetText(tmp_spread, Loc.Text("vaccine.spread"));
        SetText(tmp_log, LogText(VaccineRuntime.StageCount, true));

        foreach (var target in targets)
        {
            if (target == null) continue;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            UnityEditor.EditorUtility.SetDirty(target);
        }
    }
#endif
}
