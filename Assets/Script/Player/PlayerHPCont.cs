using UniRx;
using UnityEngine;
using System;

public class PlayerHPCont : MonoBehaviour
{
    [SerializeField] int maxHp = 100;
    [SerializeField] int currentHp = 100;

    bool inited;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public int DefaultMaxHp => maxHp;
    float HpRatio => maxHp <= 0 ? 0f : Mathf.Clamp01((float)currentHp / maxHp);

    public Subject<(int, int)> HpChanged = new Subject<(int, int)>();
    public IObservable<(int, int)> HpChangedObservable => HpChanged.AsObservable();
    public void PublishHpChanged(int oldHp, int newHp)
    {

    }



    public void Init_Once()
    {
        if (inited) return;
        inited = true;

        GameEvent.InGame.HpBarDamaged.Subscribe(OnHpBarDamaged).AddTo(this);
    }


    public void Init()
    {
        currentHp = maxHp;
        HpChanged.OnNext((maxHp, currentHp));
    }


    private void OnHpBarDamaged(int damage)
    {
        currentHp = Mathf.Max(0, currentHp - damage);
        CameraManager.Inst.Shake(CameraShakeLevel.Medium);
        HpChanged.OnNext((maxHp, currentHp));

        if (currentHp <= 0)
        {
            GameWatcher.Inst.SetGameState(GameStateType.InGame_End);
        }
    }



    public void Set_ForceEnd()
    {
        OnHpBarDamaged(maxHp);
    }
}
