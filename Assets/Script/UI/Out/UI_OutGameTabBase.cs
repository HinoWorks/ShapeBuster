using UnityEngine;
using Cysharp.Threading.Tasks;
using System;


public class UI_OutGameTabBase : MonoBehaviour
{
    // 読み込み待ちの印。今は読み込みを待つタブがない（ToOutGame_InitData を使うタブがない）ので、はじめから済みにしておく。
    // false のままだと、選んだタブの Init が待ちっぱなしになり、選択中の印（select-fix）が出なかった。
    protected bool isReloadFin = true;
    [SerializeField] protected OutGame_MenuType thisMenuType;
    public OutGame_MenuType MenuType => thisMenuType;
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
    /// 自分のタイプの時のみ、選択中の印（HButtonConnect の obj_clickSelect＝select-fix）を出す。
    /// 印はすぐに切り替え、Init_ActiveTab だけロード完了まで待つ。
    /// </summary>
    public async void Init(OutGame_MenuType _outGameMenuType)
    {
        var isActive = _outGameMenuType == thisMenuType;

        if (hButtonConnect == null)
            hButtonConnect = GetComponent<HButtonConnect>();
        if (hButtonConnect != null)
            hButtonConnect.Set_SelectActive(isActive);

        if (!isActive) return;
        if (!isReloadFin)
        {
            var canceled = await UniTask.WaitUntil(() => isReloadFin, cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (canceled) return;
        }
        Init_ActiveTab();
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
