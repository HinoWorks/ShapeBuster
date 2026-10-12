using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;



public class UI_PopUpBase : MonoBehaviour
{

    [Space(5)]
    [Header("AnimType")]
    [SerializeField] animType type;
    [SerializeField] CanvasGroup CG_target;
    protected HButton[] hButtons;
    public enum animType
    {
        scaleIn, moveIn_y, moveIn_x
    }

    private float duration_anim = 0.25f;
    private float scale_ini = 2f;
    private float moveDis = 100;
    private Vector3 posi_ini;
    private Sequence seq_open;

    protected virtual void Awake()
    {
        hButtons = this.GetComponentsInChildren<HButton>();
        AllButtonSet_StateInit();
    }

    protected virtual void AllButtonSet_StateInit()
    {
        foreach (var hButton in hButtons)
        {
            hButton.Set_StateInit();
        }
    }


    public virtual void Open()
    {
        if (seq_open == null)
        {
            CreateSeqence();
        }

        CG_target.alpha = 0f;
        this.gameObject.SetActive(true);
        seq_open.Restart();
    }

    public virtual void Close()
    {
        seq_open.Pause();
        this.gameObject.SetActive(false);
        AllButtonSet_StateInit();
    }

    private void CreateSeqence()
    {
        posi_ini = CG_target.transform.localPosition;

        seq_open = DOTween.Sequence();
        seq_open.Append(DOTween.To(() => CG_target.alpha, x => CG_target.alpha = x, 1f, duration_anim));
        switch (type)
        {
            case animType.scaleIn:
                seq_open.Join(CG_target.transform.DOScale(scale_ini * Vector3.one, 0f));
                seq_open.Join(CG_target.transform.DOScale(Vector3.one, duration_anim).SetEase(Ease.OutBack));
                break;
            case animType.moveIn_y:
                seq_open.Join(CG_target.transform.DOLocalMove(posi_ini + new Vector3(0, -moveDis, 0f), 0f));
                seq_open.Join(CG_target.transform.DOLocalMove(posi_ini, duration_anim));
                break;
            case animType.moveIn_x:
                seq_open.Join(CG_target.transform.DOLocalMove(posi_ini + new Vector3(moveDis, 0f, 0f), 0f));
                seq_open.Join(CG_target.transform.DOLocalMove(posi_ini, duration_anim));
                break;
        }
        seq_open.SetAutoKill(false).SetLink(this.gameObject).Pause();
    }




}
