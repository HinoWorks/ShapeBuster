using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;



/// <summary>
/// カーソルが乗ると大きくなり、押すと一瞬縮んでから大きく傾いて、元に戻るボタン。効果音もここで鳴らす。
/// 動きの調整値はクラスの頭の定数。
/// - 押したとき：clickSE。押せない状態（Set_Interactable(false)）で押したときは disabledClickSE。
/// - カーソルが乗ったとき：hoverSE。
/// 買う・受け取るなど、結果で音を変えるボタンは、呼び出し側で ClickSE を None にして結果の音を鳴らす。
/// </summary>
public class HButton : Button
{
    // 動きの調整値。
    const float scale_over = 1.1f;      // カーソルが乗ったときの大きさ
    const float time_over = 0.2f;       // 乗ったとき・離れたときにかける秒数
    // 押したときの動き（縮める → 広げて傾ける → 戻す）の値は UIPopAnim にある。

    Tween scaleTween;
    Tween tiltTween;

    //private HButtonConnect hButtonConnect;
    public UnityAction<bool> onMouseOver;
    public UnityAction<bool> onSelect;
    public UnityAction<bool> onInteractableChange;
    public UnityAction rightClick;
    //public UnityAction onRightClick;

    bool pointerOver;

    [Tooltip("押したときの音。None なら鳴らさない。")]
    [SerializeField] SEType clickSE = SEType.UI_Click;
    [Tooltip("カーソルが乗ったときの音。None なら鳴らさない。")]
    [SerializeField] SEType hoverSE = SEType.UI_Hover;
    [Tooltip("押せない状態で押したときの音。None なら鳴らさない。")]
    [SerializeField] SEType disabledClickSE = SEType.UI_Error;

    /// <summary>押したときの音。結果に合わせて呼び出し側で鳴らすボタンは None にする。</summary>
    public SEType ClickSE { get => clickSE; set => clickSE = value; }
    public SEType HoverSE { get => hoverSE; set => hoverSE = value; }
    public SEType DisabledClickSE { get => disabledClickSE; set => disabledClickSE = value; }




    protected override void Awake()
    {
        base.Awake();
        //hButtonConnect = this.GetComponent<HButtonConnect>();
    }

    public void Set_StateInit()
    {
        var hbutton = this.GetComponent<HButtonConnect>();
        if (hbutton != null)
        {
            hbutton.Set_StateInit();
        }
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        if (!base.interactable) return;
        base.OnPointerEnter(eventData);

        pointerOver = true;
        SoundManager.SE(hoverSE);
        ScaleTo(scale_over, time_over);
        onMouseOver?.Invoke(true);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        // interactable が先に落ちても、乗っていた表示は消す
        if (base.interactable)
            base.OnPointerExit(eventData);
        EndPointerOver();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (state == SelectionState.Disabled)
            EndPointerOver();
    }

    void EndPointerOver()
    {
        if (!pointerOver) return;
        pointerOver = false;
        ScaleTo(1f, time_over);
        onMouseOver?.Invoke(false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!base.interactable) return;
        base.OnPointerDown(eventData);
    }

    // Button is released
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (!base.interactable) return;
        base.OnPointerUp(eventData);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // 押している途中で画面が閉じても、つぶれたまま・傾いたままにしない。
        scaleTween?.Kill();
        tiltTween?.Kill();
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        pointerOver = false;
        ScaleTo(1f, time_over);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (!base.interactable)
        {
            // 親の CanvasGroup で止まっているとき（開閉の演出中など）は鳴らさない。
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && CanvasGroupAllows())
                SoundManager.SE(disabledClickSE);
            return;
        }
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (IsInteractable())
            {
                SoundManager.SE(clickSE);
                Anim_Click();
            }
            base.OnPointerClick(eventData);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (rightClick != null)
            {
                rightClick.Invoke();
            }
        }
    }


    bool CanvasGroupAllows()
    {
        var groups = GetComponentsInParent<CanvasGroup>();
        foreach (var group in groups)
        {
            if (!group.interactable) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }

    public void Set_SelectActive(bool _active)
    {
        if (onSelect != null)
        {
            onSelect(_active);
        }
    }

    public void Set_Interactable(bool _interactable)
    {
        this.interactable = _interactable;
        onInteractableChange?.Invoke(_interactable);
    }


    // ---- 動き ----
    // 乗る：scale_over へ。押せた：UIPopAnim の動きで弾ませて、元（乗っていれば scale_over）に戻す。

    float BaseScale => pointerOver ? scale_over : 1f;

    void ScaleTo(float scale, float time)
    {
        scaleTween?.Kill();
        scaleTween = transform.DOScale(scale * Vector3.one, time).SetUpdate(true);
    }

    private void Anim_Click()
    {
        scaleTween?.Kill();
        tiltTween?.Kill();
        // 大きさと傾きは別々に動かす（カーソルが外れて大きさだけ止めても、傾きは戻りきるように）。
        UIPopAnim.Play(transform, out var scale, out var tilt, BaseScale);
        scaleTween = scale;
        tiltTween = tilt;
    }
}
