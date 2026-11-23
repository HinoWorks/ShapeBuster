using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// オプションメニュー。タイトルとアウトゲームの「オプション」ボタン、または Esc キーで開く。
/// 中身：
/// - サウンド：全体・BGM・効果音の音量、消音（SoundManager）
/// - 表示：言語（Loc）、画面モード、フレームレート、数字の表し方（GameOptions）
/// - 演出：画面の揺れ、ダメージの数字（GameOptions）
/// - ゲーム：裏でも動かす（GameOptions）
/// - データ：今すぐ保存、データを消す（確認あり。消したら終了する）、ゲームを終了
/// シーンでは、このオブジェクトを非表示（非アクティブ）にして置く。開くときに自分を表示し、閉じたら非表示に戻す。
/// 非表示の間は Update が動かないので、Esc で開くのは親の Canvas にある UI_OptionsHotkey が受け持つ。
/// プレハブ（pf_ui_Options）は仮の見た目。行を並べ替えたり見た目を変えても、参照をつなぎ直せば動く。
/// </summary>
public class UI_Options : MonoBehaviour
{
    public static UI_Options Inst { get; private set; }

    [Tooltip("開いている間だけ出す、後ろを暗くする板。押すと閉じる")]
    [SerializeField] UnityEngine.UI.Button button_blocker;
    [Tooltip("ウィンドウ本体。開くときのアニメーションは UI_PopUpBase")]
    [SerializeField] UI_PopUpBase window;
    [SerializeField] HButton button_close;

    [Header("サウンド")]
    [SerializeField] UI_OptionSlider slider_master;
    [SerializeField] UI_OptionSlider slider_bgm;
    [SerializeField] UI_OptionSlider slider_se;
    [SerializeField] UI_OptionChoice choice_mute;

    [Header("表示")]
    [SerializeField] UI_OptionChoice choice_language;
    [SerializeField] UI_OptionChoice choice_screen;
    [SerializeField] UI_OptionChoice choice_frameRate;
    [SerializeField] UI_OptionChoice choice_notation;

    [Header("演出")]
    [SerializeField] UI_OptionChoice choice_shake;
    [SerializeField] UI_OptionChoice choice_damageNumber;

    [Header("ゲーム")]
    [SerializeField] UI_OptionChoice choice_background;

    [Header("データ")]
    [SerializeField] HButton button_save;
    [SerializeField] HButton button_delete;
    [SerializeField] HButton button_quit;
    [Tooltip("「保存しました」などの短いお知らせ")]
    [SerializeField] TextMeshProUGUI tmp_status;
    [SerializeField] TextMeshProUGUI tmp_version;

    [Header("データを消す確認")]
    [SerializeField] GameObject obj_confirm;
    [SerializeField] TextMeshProUGUI tmp_confirm;
    [SerializeField] HButton button_confirmYes;
    [SerializeField] HButton button_confirmNo;

    const float StatusDuration = 1.5f;
    const float QuitDelay = 1.8f;

    UI_OptionChoice[] choices;
    UI_OptionSlider[] sliders;
    CancellationTokenSource statusCts;
    bool deleting;
    bool bound;
    int openedFrame = -1;

    public bool IsOpen => gameObject.activeSelf && window != null && window.gameObject.activeSelf;

    void Awake()
    {
        BindOnce();
    }

    /// <summary>最初に表示されたとき（Awake）か、最初に Open されたときに一度だけつなぐ。</summary>
    void BindOnce()
    {
        if (bound) return;
        bound = true;
        Inst = this;

        choices = new[] { choice_mute, choice_language, choice_screen, choice_frameRate, choice_notation, choice_shake, choice_damageNumber, choice_background };
        sliders = new[] { slider_master, slider_bgm, slider_se };
        Bind();

        if (window != null) window.gameObject.SetActive(false);
        if (button_blocker != null) button_blocker.gameObject.SetActive(false);
        if (obj_confirm != null) obj_confirm.SetActive(false);

        GameEvent.GameState.SetGameState
            .Subscribe(_ => { if (IsOpen) Close(); })
            .AddTo(this);
        Loc.Changed += OnLanguageChanged;
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
        statusCts?.Cancel();
        if (Inst == this) Inst = null;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        // 同じフレームに Esc で開いたばかりなら閉じない。
        if (!IsOpen || Time.frameCount == openedFrame) return;
        if (obj_confirm != null && obj_confirm.activeSelf) CloseConfirm();
        else Close();
    }

    #region -- 開く・閉じる --
    /// <summary>オプションを開いてよい場面か。タイトルとアウトゲームだけ。画面の切り替え中やワクチンの演出中は開かない。</summary>
    public static bool CanOpen()
    {
        var watcher = GameWatcher.Inst;
        if (watcher == null || watcher.IsChangingScreen || VaccineEnding.Playing) return false;
        var state = watcher.currentGameState;
        return state == GameStateType.Title || state == GameStateType.OutGame;
    }

    /// <summary>ボタンから呼ぶ。</summary>
    public void Open()
    {
        if (IsOpen || deleting || !CanOpen()) return;
        openedFrame = Time.frameCount;
        // 非表示で置いてあるので、ここで表示する（初めてなら Awake でつなぐ）。
        gameObject.SetActive(true);
        BindOnce();
        RefreshAll();
        if (obj_confirm != null) obj_confirm.SetActive(false);
        if (tmp_status != null) tmp_status.SetText("");
        if (button_blocker != null) button_blocker.gameObject.SetActive(true);
        if (window != null) window.Open();
    }

    public void Close()
    {
        if (deleting) return;
        if (button_blocker != null) button_blocker.gameObject.SetActive(false);
        if (obj_confirm != null) obj_confirm.SetActive(false);
        if (window != null && window.gameObject.activeSelf) window.Close();
        gameObject.SetActive(false);
    }
    #endregion

    #region -- つなぐ --
    void Bind()
    {
        if (button_blocker != null) button_blocker.onClick.AddListener(Close);
        if (button_close != null) button_close.onClick.AddListener(Close);

        slider_master?.Setup(() => SoundManager.Inst != null ? SoundManager.Inst.VolumeMaster : 1f, v => SoundManager.Inst?.SetVolumeMaster(v));
        slider_bgm?.Setup(() => SoundManager.Inst != null ? SoundManager.Inst.VolumeBGM : 1f, v => SoundManager.Inst?.SetVolumeBGM(v));
        slider_se?.Setup(() => SoundManager.Inst != null ? SoundManager.Inst.VolumeSE : 1f, v => SoundManager.Inst?.SetVolumeSE(v));
        choice_mute?.Setup(
            () => OnOff(SoundManager.Inst != null && SoundManager.Inst.Muted),
            _ => SoundManager.Inst?.SetMuted(!(SoundManager.Inst.Muted)));

        choice_language?.Setup(LanguageText, d =>
        {
            int count = System.Enum.GetValues(typeof(GameLanguage)).Length;
            Loc.SetLanguage((GameLanguage)Wrap((int)Loc.Language + d, count));
        });
        choice_screen?.Setup(
            () => Loc.Text(GameOptions.Fullscreen ? "option.screen.full" : "option.screen.window"),
            _ => GameOptions.SetFullscreen(!GameOptions.Fullscreen));
        choice_frameRate?.Setup(FrameRateText,
            d => GameOptions.SetFrameRateIndex(Wrap(GameOptions.FrameRateIndex + d, GameOptions.FrameRates.Length)));
        choice_notation?.Setup(
            () => Loc.Text(GameOptions.Notation == NumberNotation.Scientific ? "option.notation.scientific" : "option.notation.short"),
            _ => GameOptions.SetNotation(GameOptions.Notation == NumberNotation.Short ? NumberNotation.Scientific : NumberNotation.Short));

        choice_shake?.Setup(() => OnOff(GameOptions.ScreenShake), _ => GameOptions.SetScreenShake(!GameOptions.ScreenShake));
        choice_damageNumber?.Setup(() => OnOff(GameOptions.DamageNumbers), _ => GameOptions.SetDamageNumbers(!GameOptions.DamageNumbers));
        choice_background?.Setup(() => OnOff(GameOptions.RunInBackground), _ => GameOptions.SetRunInBackground(!GameOptions.RunInBackground));

        if (button_save != null) { button_save.ClickSE = SEType.None; button_save.onClick.AddListener(OnClickSave); }
        if (button_delete != null) button_delete.onClick.AddListener(OpenConfirm);
        if (button_quit != null) button_quit.onClick.AddListener(QuitGame);
        if (button_confirmYes != null) { button_confirmYes.ClickSE = SEType.None; button_confirmYes.onClick.AddListener(OnClickDeleteYes); }
        if (button_confirmNo != null) button_confirmNo.onClick.AddListener(CloseConfirm);
    }

    void OnLanguageChanged(GameLanguage _)
    {
        RefreshAll();
    }

    void RefreshAll()
    {
        if (choices != null)
            foreach (var choice in choices) choice?.Refresh();
        if (sliders != null)
            foreach (var slider in sliders) slider?.Refresh();
        if (tmp_version != null)
        {
            Loc.UseFont(tmp_version);
            tmp_version.SetText(Loc.Format("option.version", Application.version));
        }
        if (tmp_confirm != null)
        {
            Loc.UseFont(tmp_confirm);
            tmp_confirm.SetText(Loc.Text("option.delete.confirm"));
        }
    }

    static string OnOff(bool on) => Loc.Text(on ? "option.on" : "option.off");

    static string LanguageText()
    {
        // 言語の名前は、どの言語のときもその言語の書き方で出す。
        return Loc.Language == GameLanguage.En ? "English" : "日本語";
    }

    static string FrameRateText()
    {
        int rate = GameOptions.FrameRates[GameOptions.FrameRateIndex];
        return rate <= 0 ? Loc.Text("option.frameRate.vsync") : rate.ToString();
    }

    static int Wrap(int value, int count) => ((value % count) + count) % count;
    #endregion

    #region -- データ --
    void OnClickSave()
    {
        if (SaveLoader.Inst == null)
        {
            SoundManager.SE(SEType.UI_Error);
            return;
        }
        SaveLoader.Inst.SaveNow();
        SoundManager.SE(SEType.UI_Purchase);
        ShowStatus(Loc.Text("option.save.done"));
    }

    void OpenConfirm()
    {
        if (obj_confirm == null) return;
        RefreshAll();
        obj_confirm.SetActive(true);
    }

    void CloseConfirm()
    {
        if (deleting || obj_confirm == null) return;
        obj_confirm.SetActive(false);
    }

    void OnClickDeleteYes()
    {
        if (deleting || SaveLoader.Inst == null) return;
        deleting = true;
        SoundManager.SE(SEType.UI_Error);
        SaveLoader.Inst.DeleteAllData();
        if (tmp_confirm != null)
        {
            Loc.UseFont(tmp_confirm);
            tmp_confirm.SetText(Loc.Text("option.delete.done"));
        }
        if (button_confirmYes != null) button_confirmYes.gameObject.SetActive(false);
        if (button_confirmNo != null) button_confirmNo.gameObject.SetActive(false);
        QuitLater().Forget();
    }

    async UniTaskVoid QuitLater()
    {
        await UniTask.Delay((int)(QuitDelay * 1000f), DelayType.UnscaledDeltaTime);
        QuitGame();
    }

    /// <summary>残りを保存してゲームを終了する。タイトルの終了ボタンからも呼ぶ。</summary>
    public static void QuitGame()
    {
        // 終了する前に残りを書き出す（データを消したあとは何も書かない）。
        if (SaveLoader.Inst != null) SaveLoader.Inst.SaveNow();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowStatus(string text)
    {
        if (tmp_status == null) return;
        statusCts?.Cancel();
        statusCts = new CancellationTokenSource();
        Loc.UseFont(tmp_status);
        tmp_status.SetText(text);
        ClearStatusLater(statusCts.Token).Forget();
    }

    async UniTaskVoid ClearStatusLater(CancellationToken token)
    {
        bool canceled = await UniTask.Delay((int)(StatusDuration * 1000f), DelayType.UnscaledDeltaTime, cancellationToken: token).SuppressCancellationThrow();
        if (!canceled && tmp_status != null) tmp_status.SetText("");
    }
    #endregion
}
