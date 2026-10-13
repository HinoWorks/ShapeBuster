using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

/// <summary>
/// プレイヤーHPゲージ。緑を先に減らし、遅れてオレンジを減らしてダメージ差分を見せる。
/// </summary>
public class UI_PlayerHpCont : MonoBehaviour
{
    [SerializeField] Image greenFill;
    [SerializeField] Image orangeFill;
    [SerializeField] float greenDuration = 0.15f;
    [SerializeField] float orangeDelay = 0.5f;
    [SerializeField] float orangeDuration = 0.35f;


    public void InitOnce()
    {
        PlayerManager.Inst.PlayerHPCont.HpChangedObservable.Subscribe(x => OnHpChanged(x.Item1, x.Item2)).AddTo(this);
    }

    private void OnHpChanged(int oldHp, int newHp)
    {
        SetRatio((float)newHp / oldHp);
    }

    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        TweenFill(greenFill, ratio, greenDuration, 0f);

        bool showDamageGap = orangeFill != null && ratio < orangeFill.fillAmount;
        if (showDamageGap)
            TweenFill(orangeFill, ratio, orangeDuration, orangeDelay);
        else
            TweenFill(orangeFill, ratio, greenDuration, 0f);
    }

    public void SetRatioImmediate(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        ApplyImmediate(greenFill, ratio);
        ApplyImmediate(orangeFill, ratio);
    }

    void OnDisable()
    {
        greenFill?.DOKill();
        orangeFill?.DOKill();
    }

    void TweenFill(Image fill, float ratio, float duration, float delay)
    {
        if (fill == null) return;

        fill.DOKill();
        fill.DOFillAmount(ratio, duration)
            .SetDelay(delay)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    static void ApplyImmediate(Image fill, float ratio)
    {
        if (fill == null) return;

        fill.DOKill();
        fill.fillAmount = ratio;
    }
}
