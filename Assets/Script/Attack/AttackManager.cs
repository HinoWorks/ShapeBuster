using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

public class AttackManager : MonoBehaviour
{

    public static AttackManager Inst;
    private AttackCont_Cursor cursorCont;
    // [SerializeField] List<AttackContBase> attackConts = new List<AttackContBase>();
    //[SerializeField] PickaxeAnimCont pickaxeAnimCont;
    private bool isAttacking = false;
    public int currentPickaxeDamage => cursorCont.Damage;
    public Transform CursorTransform => cursorCont != null ? cursorCont.CursorTransform : null;

    private readonly Subject<Unit> onPickaxeAttackTiming = new();
    public IObservable<Unit> OnPickaxeAttackTiming => onPickaxeAttackTiming.AsObservable();


    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }
        onPickaxeAttackTiming.AddTo(this);
    }


    public async void Set_Ready()
    {
        isAttacking = false;

        /*
                var cursorUnitData = GameParamManager.Get_CursorParam(cursorSlotData.equipedCursorIndex);
                if (cursorUnitData != null)
                {
                    CursorUnitGenerate(cursorUnitData);
                }

                // 攻撃ユニット生成 == スキルツリー分のパラメータを読み込む
                foreach (var attackParam in GameParamManager.list_attackParam)
                {
                    if (!attackParam.isActive) continue;
                    AttackUnitGenerate(attackParam);
                }
                */


        var cursorData = SOLoader.CursorUnitData.GetCursorUnitData(0);
        var cursorParam = new CursorParam()
        {
            so = cursorData,
            damage = cursorData.damage,
            attackInterval = cursorData.attackInterval,
            criticalRate = cursorData.criticalRate,
            resourceUpRate = cursorData.resourceUpRate,
            size = cursorData.size
        };
        CursorUnitGenerate(cursorParam);
        cursorCont.Set_ActiveCursor(true);
    }


    private void CursorUnitGenerate(CursorParam _cursorParam)
    {
        var cursorUnit = Instantiate(_cursorParam.so.pf, transform) as GameObject;
        cursorUnit.transform.position = transform.position;

        cursorCont = cursorUnit.GetComponent<AttackCont_Cursor>();
        cursorCont.Init(_cursorParam);
        cursorCont.Set_AttackTrigger(isAttacking);
    }


    public void NotifyPickaxeAttackTiming()
    {
        onPickaxeAttackTiming.OnNext(Unit.Default);
    }


    private void AttackUnitGenerate(AttackParam _attackParam)
    {
        /*
        var attackUnit = Instantiate(_attackParam.so.pf, transform) as GameObject;
        attackUnit.transform.position = transform.position;
        attackUnit.transform.localScale = Vector3.one;

        var attackCont = attackUnit.GetComponent<AttackContBase>();
        attackConts.Add(attackCont);
        attackCont.Init(_attackParam);
        attackCont.Set_AttackTrigger(isAttacking);
        */
    }


    public void Set_AttackState(bool isStart)
    {
        // 攻撃開始
        isAttacking = isStart;
        cursorCont.Set_AttackTrigger(isStart);
    }


    public void AttackUnitDelete()
    {
        /*
        foreach (var attackCont in attackConts)
        {
            attackCont.OnDestroy();
        }
        attackConts.Clear();
        */


        cursorCont.OnDestroy();
    }







}
