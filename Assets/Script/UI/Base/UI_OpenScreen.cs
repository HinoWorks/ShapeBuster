using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;


[RequireComponent(typeof(CanvasGroup))]
public class UI_OpenScreen : MonoBehaviour
{
    private CanvasGroup CG;
    [SerializeField] float duration = 1f;

    public void ScreenON()
    {
        if (CG == null)
        {
            CG = this.gameObject.GetComponent<CanvasGroup>();
        }
        CG.alpha = 1f;
        this.gameObject.SetActive(true);
    }


    public void ScreenFeedOut()
    {
        DOTween.To(() => CG.alpha, x => CG.alpha = x, 0f, duration)
        .OnComplete(() =>
        {
            this.gameObject.SetActive(false);
        }).Play();
    }



#if UNITY_EDITOR
    [SerializeField] bool _isOn;
    private void OnValidate()
    {
        if (_isOn)
        {
            _isOn = false;
            ButtonProcess();
        }
    }
    private void ButtonProcess()
    {
        ScreenON();
        ScreenFeedOut();
    }
#endif


}
