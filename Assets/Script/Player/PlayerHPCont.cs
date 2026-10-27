using UniRx;
using UnityEngine;
using System;

public class PlayerHPCont : MonoBehaviour
{
    [SerializeField] int maxHp = 100;
    [SerializeField] int currentHp = 100;

    bool inited;
    float regenStock;

    public int MaxHp => Mathf.Max(1, maxHp + SkillTreeBonus.PlayerMaxHp + MilestoneBonus.PlayerMaxHp);
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
        currentHp = MaxHp;
        regenStock = 0f;
        HpChanged.OnNext((MaxHp, currentHp));
    }

    void Update()
    {
        if (GameWatcher.Inst == null || !GameWatcher.Inst.isInGameNow)
        {
            regenStock = 0f;
            return;
        }
        if (currentHp <= 0 || currentHp >= MaxHp) return;

        float perSecond = SkillTreeBonus.Regen;
        if (perSecond <= 0f) return;

        regenStock += perSecond * Time.deltaTime;
        if (regenStock < 1f) return;

        int heal = Mathf.FloorToInt(regenStock);
        regenStock -= heal;
        currentHp = Mathf.Min(MaxHp, currentHp + heal);
        HpChanged.OnNext((MaxHp, currentHp));
    }


    private void OnHpBarDamaged(int damage)
    {
        ApplyIncoming(damage, true);
    }

    public void Set_ForceEnd()
    {
        ApplyIncoming(MaxHp, false);
    }

    void ApplyIncoming(int damage, bool useArmor)
    {
        if (useArmor)
            damage = SkillTreeBonus.ApplyArmor(damage);
        if (AttackManager.Inst != null && AttackManager.Inst.IsCursorReloading)
            damage = SkillTreeBonus.ApplyReloadGuard(damage);
        if (damage <= 0) return;

        GameEvent.InGame.PublishDamageTaken(damage);
        currentHp = Mathf.Max(0, currentHp - damage);
        CameraManager.Inst.Shake(CameraShakeLevel.Medium);
        HpChanged.OnNext((MaxHp, currentHp));

        if (currentHp <= 0)
        {
            GameWatcher.Inst.SetGameState(GameStateType.InGame_End);
        }
    }
}
