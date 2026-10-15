using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ステージ経過時間の割合。ゲージとパーセントで出す。
/// </summary>
public class UI_stageProgressCont : MonoBehaviour
{
    [SerializeField] Image fill;
    [SerializeField] TextMeshProUGUI rateLabel;

    private bool isInGame = false;
    int shownPercent = -1;

    public void InitOnce()
    {
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
        Refresh();
    }

    void Update()
    {
        if (!isInGame) return;
        Refresh();
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
                SetRatio(0f);
                break;
            case GameStateType.InGame:
                isInGame = true;
                break;
            case GameStateType.InGame_End:
                isInGame = false;
                break;
        }
    }

    void Refresh()
    {
        var stage = StageTimeManager.Inst;
        if (stage == null || stage.stageMaxTime <= 0f)
        {
            SetRatio(0f);
            return;
        }

        SetRatio(stage.stageTime / stage.stageMaxTime);
    }

    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (fill != null)
            fill.fillAmount = ratio;

        int percent = Mathf.RoundToInt(ratio * 100f);
        if (rateLabel == null || percent == shownPercent) return;

        shownPercent = percent;
        rateLabel.text = $"{percent}%";
    }
}
