using System.Text;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アウトゲームのワクチンタブ。ゲージ・注ぐボタン・工程の記録（研究の成果と日記）・クリアの印を出す。
/// 注ぐボタンは連打する前提で、押すたびに VaccineRuntime.TryPour を呼ぶ。
/// 満タンになったら「ワクチンを撒く」を出し、押すと撒く演出のあとタイトルへ戻る（VaccineEnding）。撒けるのは一度だけ。
/// </summary>
public class UI_VaccineManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI tmp_title;
    [Tooltip("ゲージの中身。Image の Fill Amount で進み具合を出す")]
    [SerializeField] Image image_gaugeFill;
    [SerializeField] TextMeshProUGUI tmp_progress;
    [SerializeField] HButton button_pour;
    [SerializeField] TextMeshProUGUI tmp_pour;
    [SerializeField] HButton button_spread;
    [SerializeField] TextMeshProUGUI tmp_spread;
    [Tooltip("工程の記録。新しい工程が終わったら一番下まで送る")]
    [SerializeField] TextMeshProUGUI tmp_log;
    [SerializeField] ScrollRect logScroll;
    [Tooltip("クリアしたあとに出す印")]
    [SerializeField] GameObject obj_clearMark;
    [SerializeField] TextMeshProUGUI tmp_clearMark;

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
        SetText(tmp_progress, Loc.Format("vaccine.progress", Mathf.RoundToInt(VaccineRuntime.Progress * 100f)));
        RefreshPour();

        bool full = VaccineRuntime.IsFull;
        if (button_spread != null)
        {
            button_spread.gameObject.SetActive(full);
            // 撒くのは一度だけ。撒いたあとは押せなくする。
            button_spread.Set_Interactable(!VaccineRuntime.Cleared);
        }
        SetText(tmp_spread, Loc.Text(VaccineRuntime.Cleared ? "vaccine.spread.done" : "vaccine.spread"));

        if (obj_clearMark != null)
            obj_clearMark.SetActive(VaccineRuntime.Cleared);
        SetText(tmp_clearMark, Loc.Text("vaccine.clearMark"));

        RefreshLog();
    }

    void RefreshPour()
    {
        if (button_pour == null) return;
        bool full = VaccineRuntime.IsFull;
        button_pour.gameObject.SetActive(!full);
        if (full || tmp_pour == null) return;

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
        shownStages = key;

        SetText(tmp_log, LogText(done, cleared));
        if (logScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            // 新しい行が増えたら一番下へ、開き直したときは一番上から読む。
            logScroll.verticalNormalizedPosition = grew ? 0f : 1f;
        }
    }

    /// <summary>工程の記録。終わった工程ごとに、名前・研究の成果・日記を並べる。</summary>
    static string LogText(int done, bool cleared)
    {
        var sb = new StringBuilder();
        if (done <= 0)
        {
            sb.Append(Loc.Text("vaccine.stage0"));
            return sb.ToString();
        }
        for (int i = 1; i <= done; i++)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("<b>").Append(Loc.Text("vaccine.stage" + i + ".name")).Append("</b>\n");
            sb.Append(Loc.Text("vaccine.stage" + i + ".result")).Append('\n');
            sb.Append(Loc.Text("vaccine.stage" + i + ".diary"));
        }
        if (cleared)
        {
            sb.Append("\n\n<b>").Append(Loc.Text("vaccine.clear.name")).Append("</b>\n");
            sb.Append(Loc.Text("vaccine.clear.diary"));
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
        if (!VaccineRuntime.IsFull || VaccineRuntime.Cleared) return;
        VaccineEnding.Play();
    }

    static void SetText(TextMeshProUGUI label, string text)
    {
        if (label == null) return;
        Loc.UseFont(label);
        label.SetText(text);
    }
}
