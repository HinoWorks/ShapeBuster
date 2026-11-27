using DG.Tweening;
using UniRx;
using UnityEngine;

/// <summary>
/// インゲームの操作ガイド（obj_HelpGuide）の出し入れ。
/// ステージ 0 は出したまま。それ以降は、ステージの最初の数秒だけ出してからフェードで消す。
/// ヘルプボタンを押すと、いつでも出す／消すを切り替えられる（押したあとは自動では消さない）。
/// </summary>
public class UI_HelpGuide : MonoBehaviour
{
    [SerializeField] GameObject guide;
    [SerializeField] HButton helpButton;
    [Tooltip("ステージ 1 以降で、ガイドを出しておく秒数（ステージの経過時間）")]
    [SerializeField] float showTime = 5f;
    [Tooltip("消えるときのフェードの秒数")]
    [SerializeField] float fadeTime = 0.5f;

    CanvasGroup group;
    Tween fade;
    bool autoHide;

    void Awake()
    {
        if (guide != null)
        {
            group = guide.GetComponent<CanvasGroup>();
            if (group == null) group = guide.AddComponent<CanvasGroup>();
            // ガイドが出ていても、下のフィールドでボールを撃てるようにする。
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        if (helpButton != null)
            helpButton.onClick.AddListener(OnClick_Help);

        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
    }

    void OnDestroy()
    {
        fade?.Kill();
    }

    void OnGameState(GameStateType state)
    {
        if (state != GameStateType.InGame_Ready) return;

        int stage = StageTimeManager.Inst != null ? StageTimeManager.Inst.StageIndex : 0;
        Show();
        // ステージ 0 は出したまま。それ以降は showTime 秒たったら消す。
        autoHide = stage > 0;
    }

    void Update()
    {
        if (!autoHide) return;
        var time = StageTimeManager.Inst;
        if (time == null || !time.isTimeRunning) return;
        if (time.stageTime < showTime) return;

        autoHide = false;
        FadeOut();
    }

    void Show()
    {
        if (guide == null) return;
        fade?.Kill();
        group.alpha = 1f;
        guide.SetActive(true);
    }

    void FadeOut()
    {
        if (guide == null || !guide.activeSelf) return;
        fade?.Kill();
        fade = group.DOFade(0f, fadeTime)
            .SetUpdate(true)
            .OnComplete(() => guide.SetActive(false));
    }

    void Hide()
    {
        if (guide == null) return;
        fade?.Kill();
        guide.SetActive(false);
    }

    public void OnClick_Help()
    {
        if (guide == null) return;
        // 押したら、そのランでは自動で消さない。
        autoHide = false;
        // フェード中に押したときは、消えかけを出し直す。
        bool fading = fade != null && fade.IsActive() && fade.IsPlaying();
        if (guide.activeSelf && !fading) Hide();
        else Show();
    }
}
