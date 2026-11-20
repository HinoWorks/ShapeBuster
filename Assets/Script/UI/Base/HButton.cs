using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;



/// <summary>
/// 押したときに縮み、カーソルが乗ると大きくなるボタン。効果音もここで鳴らす。
/// - 押したとき：clickSE。押せない状態（Set_Interactable(false)）で押したときは disabledClickSE。
/// - カーソルが乗ったとき：hoverSE。
/// 買う・受け取るなど、結果で音を変えるボタンは、呼び出し側で ClickSE を None にして結果の音を鳴らす。
/// </summary>
public class HButton : Button
{
    const float duration_scaleChange = 0.2f;
    const float scale_push = 0.85f; // ボタンが押されたときのスケール
    const float scale_over = 1.1f; // ボタンにカーソルが乗ったときのスケール

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
        Anim_ScaleChange_PointerOn();
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
        Anim_ScaleChange_ToNormal();
        onMouseOver?.Invoke(false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (!base.interactable) return;
        Anim_ScaleChange_ToSmall();
        base.OnPointerDown(eventData);
    }

    // Button is released
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (!base.interactable) return;
        Anim_ScaleChange_ToNormal();
        base.OnPointerUp(eventData);
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
                SoundManager.SE(clickSE);
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


    private void Anim_ScaleChange_PointerOn()
    {
        this.gameObject.transform.DOScale(scale_over * Vector3.one, duration_scaleChange).SetUpdate(true).Play();
    }
    private void Anim_ScaleChange_ToSmall()
    {
        this.gameObject.transform.DOScale(scale_push * Vector3.one, duration_scaleChange).SetUpdate(true).Play();
    }
    private void Anim_ScaleChange_ToNormal()
    {
        this.gameObject.transform.DOScale(1f * Vector3.one, duration_scaleChange).SetUpdate(true).Play();
    }
}



