using UnityEngine;
using Cysharp.Threading.Tasks;
using System;


public class UI_OutGameTabBase : MonoBehaviour
{
    protected bool isReloadFin = false;
    [SerializeField] protected OutGame_MenuType thisMenuType;
    private HButtonConnect hButtonConnect;


    void Awake()
    {
        hButtonConnect = this.GetComponent<HButtonConnect>();
        if (thisMenuType == OutGame_MenuType.Loading)
        {
            this.gameObject.SetActive(false);
        }
    }
    /// <summary>
    /// 初期化時に一度だけ呼ばれる.主にコールバックを設定
    /// </summary>
    public virtual void Start_OnceInit() { }


    /// <summary>
    /// 自分のタイプの時のみ、アクティブにする。　ロード完了まで待機
    /// </summary>
    public async void Init(OutGame_MenuType _outGameMenuType)
    {
        var isActive = _outGameMenuType == thisMenuType;

        if (isActive)
        {
            await UniTask.WaitUntil(() => isReloadFin);
            Init_ActiveTab();
        }
        hButtonConnect.Set_SelectActive(isActive);
        //        this.gameObject.SetActive(isActive);
    }

    /// <summary>
    /// アウトゲームに移行した時、一度だけデータを更新する
    /// </summary>
    public virtual async void ToOutGame_InitData() { }


    /// <summary>
    /// インゲームに移行した時、ロードフラグをリセットする
    /// </summary>
    public void ToInGame_ResetLoadFlag()
    {
        isReloadFin = false;
    }

    /// <summary>
    /// 自身のタブメニューがアクティブになった時、毎回呼ばれる
    /// </summary>
    protected virtual void Init_ActiveTab() { }


    public void OnClick_TabChange()
    {
        UI_OutGameManager.Inst.ActiveTabChange(thisMenuType);
    }

}
