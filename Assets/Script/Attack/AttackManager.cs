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

    private SO_AttackUnitData attackUnitData => SOLoader.AttackUnitData;
    [SerializeField] int[] debugAttackIndices = { 0, 1, 2, 3 };
    readonly List<AttackPatternBase> spawnedPatterns = new();


    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }
        onPickaxeAttackTiming.AddTo(this);
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
    }



    private void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
                Set_Ready();
                break;
            case GameStateType.InGame:
                Set_AttackState(true);
                break;

            case GameStateType.InGame_End:
            case GameStateType.Title:
            case GameStateType.OutGame:
                Set_AttackState(false);
                ClearAttackPatterns();
                break;
        }
    }


    public async void Set_Ready()
    {
        isAttacking = false;
        AttackUnitDelete();

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
            damage = cursorData.damage + SkillTreeBonus.CursorDamage,
            attackInterval = Mathf.Max(0.05f, cursorData.attackInterval + SkillTreeBonus.CursorAttackInterval),
            criticalRate = cursorData.criticalRate + SkillTreeBonus.CursorCriticalRate,
            resourceUpRate = cursorData.resourceUpRate + SkillTreeBonus.CursorResourceUpRate,
            size = Mathf.Max(0.01f, cursorData.size + SkillTreeBonus.CursorSize),
            magazineAmmo = Mathf.Max(1, cursorData.magazineAmmo),
            magazineCount = Mathf.Max(1, cursorData.magazineCount),
            reloadTime = cursorData.reloadTime > 0f ? cursorData.reloadTime : 1.2f
        };
        CursorUnitGenerate(cursorParam);
        cursorCont.Set_ActiveCursor(true);
        RefreshAttackPatterns();
    }

    void RefreshAttackPatterns()
    {
        ClearAttackPatterns();
        if (attackUnitData == null)
        {
            Debug.LogWarning("攻撃ユニットの SO が未設定です。");
            return;
        }
        if (debugAttackIndices == null) return;

        for (int i = 0; i < debugAttackIndices.Length; i++)
        {
            int index = debugAttackIndices[i];
            var data = attackUnitData.Find(index);
            if (data == null)
            {
                Debug.LogWarning($"攻撃ユニットがありません。 index: {index}");
                continue;
            }
            if (data.pf == null)
            {
                Debug.LogWarning($"攻撃プレハブが未設定です。 index: {index}");
                continue;
            }

            var instance = Instantiate(data.pf, transform);
            spawnedPatterns.Add(instance);
            instance.Begin(SkillTreeBonus.WithAttackBonus(data));
        }
    }

    void ClearAttackPatterns()
    {
        for (int i = spawnedPatterns.Count - 1; i >= 0; i--)
        {
            var pattern = spawnedPatterns[i];
            if (pattern == null) continue;
            pattern.End();
            Destroy(pattern.gameObject);
        }
        spawnedPatterns.Clear();
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
        isAttacking = isStart;
        if (cursorCont != null)
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


        ClearAttackPatterns();
        if (cursorCont != null)
        {
            cursorCont.OnDestroy();
            cursorCont = null;
        }
    }


}
