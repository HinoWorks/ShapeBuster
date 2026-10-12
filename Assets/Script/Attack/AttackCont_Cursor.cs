using UnityEngine;
using UniRx;
using System.Collections.Generic;
using System;
using DG.Tweening;
using Unity.VisualScripting;
using Cysharp.Threading.Tasks;
using System.Threading;

public class AttackCont_Cursor : MonoBehaviour
{
    protected CursorParam cursorParam;

    protected bool isActive = false;//　Init後、攻撃開始タイミング同期用。trueになったら攻撃開始

    public int baseDamage => cursorParam.damage;
    protected int damage => 1;//(int)(cursorParam.damage * (1f + GameParamManager.gameBaseParam.pickaxeBase_AttackDamage));
    protected float attackInterval => cursorParam.attackInterval;
    protected float criticalRate => cursorParam.criticalRate + 0f;
    protected float resourceUpRate_pickaxe => cursorParam.resourceUpRate + 0f;
    protected float size => cursorParam.size;


    [SerializeField] GameObject obj_pointerArea;
    [SerializeField] TriggerSender[] triggerSender;
    [SerializeField] ParticleSystem eff_critical;



    // 複数 TriggerSender で同一対象と重なるとき、片方の Exit だけでは対象を外さない
    private readonly Dictionary<IDamagable, int> targetOverlapRefCount = new Dictionary<IDamagable, int>();
    private HashSet<IDamagable> targets = new HashSet<IDamagable>();
    private readonly List<IDamagable> removeBuffer = new();

    public Vector3 pickaxePosition => obj_pointerArea.transform.position;
    private Vector3 offsetPosition = new Vector3(0, 0.1f, 0);
    private float calc_criticalDamageRate = 1f;
    private float criticalDamageRate = 2f;
    private int instantShatterDamage = 9999;

    private readonly SerialDisposable attackLoopDisposable = new SerialDisposable();

    private float timer = 0;
    private CancellationTokenSource CTS;



    protected void Awake()
    {
        attackLoopDisposable.AddTo(this);
        GameEvent.Input.PointerAreaIn.Subscribe(isAreaIn => PointerAreaIn(isAreaIn)).AddTo(this);
        GameEvent.Input.PointerMove.Subscribe(pos => PointerMove(pos)).AddTo(this);
        foreach (var sender in triggerSender)
        {
            sender.OnEnter += OnEnter;
            sender.OnExit += OnExit;
        }
    }
    public void Init(CursorParam _cursorParam)
    {
        cursorParam = _cursorParam;
        obj_pointerArea.transform.localScale = size * Vector3.one;
        targets.Clear();
        targetOverlapRefCount.Clear();
        //CreateAttackRoop();
        this.gameObject.SetActive(false);
        timer = 0;
        CTS = new CancellationTokenSource();

#if UNITY_EDITOR
        DebugLog();
#endif
    }

    public virtual void Set_ActiveCursor(bool isActiveCursor)
    {
        this.gameObject.SetActive(isActiveCursor);
    }
    public virtual void Set_AttackTrigger(bool isTrigger)
    {
        isActive = isTrigger;
    }


    public void OnDestroy()
    {
        foreach (var sender in triggerSender)
        {
            sender.OnEnter -= OnEnter;
            sender.OnExit -= OnExit;
        }
        CTS.Cancel();
        Destroy(this.gameObject);
    }

    void Update()
    {
        if (!isActive) return;
        timer += Time.deltaTime;
        if (timer >= attackInterval)
        {
            timer = 0;
            Attack();
        }
    }
    private async void Attack()
    {
        AttackManager.Inst.NotifyPickaxeAttackTiming();
        removeBuffer.Clear();

        // critical check
        var isCritical = UnityEngine.Random.Range(0f, 1f) < criticalRate;
        //isCritical = UnityEngine.Random.Range(0f, 1f) < 0.35f; // for DEBUG
        calc_criticalDamageRate = isCritical ? criticalDamageRate : 1f;

        if (isCritical)
        {
            eff_critical.Play();
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: CTS.Token);

        obj_pointerArea.transform.DOScale(1.1f * size * Vector3.one, 0.075f).SetEase(Ease.OutBack);
        obj_pointerArea.transform.DOScale(size * Vector3.one, 0.075f).SetEase(Ease.OutBack).SetDelay(0.075f)
            .OnComplete(() =>
            {

            });

        foreach (var t in targets)
        {
            if (!t.isAlive)
            {
                removeBuffer.Add(t);
                continue;
            }

            /*
                        // instant shatter check
                        var damage_calc = GameParamManager.gameBaseParam.isInstantShatter ?
                                                instantShatterDamage : (int)(damage * calc_criticalDamageRate);

                        // lucky mine check
                        var isLuckyMine = GameParamManager.gameBaseParam.isLuckyMine;
                        var resourceUpRate_LuckyMine = isLuckyMine ? GameParamManager.gameBaseParam.luckyMineRate_ResourceUpRate : 0f;

                        if (t.Damage(damage_calc, resourceUpRate_pickaxe
                                            + resourceUpRate_LuckyMine
                                            + GameParamManager.gameBaseParam.resourceUpRate))
                                            */

            // 破壊されていた場合
            removeBuffer.Add(t);

        }
        if (targets.Count > 0)
        {
            GameEvent.InGame.PublishOnPickaxeAttack();
            Check_AttackAddTime();
        }
        foreach (var t in removeBuffer)
        {
            targets.Remove(t);
            targetOverlapRefCount.Remove(t);
        }
    }

    private void Check_AttackAddTime()
    {
        //if (GameParamManager.gameBaseParam.isPickaxeAttack_AddIngameTime)
        //{
        //    InGameManager.Inst.AddGetExTime(1f);
        //}
    }



    #region -- position fix --
    private void PointerAreaIn(bool isAreaIn)
    {
        if (isAreaIn == obj_pointerArea.activeSelf) return;
        obj_pointerArea.SetActive(isAreaIn);
    }

    private void PointerMove(Vector3 pos)
    {
        obj_pointerArea.transform.position = pos + offsetPosition;
    }
    #endregion


    #region -- target fix --
    private void OnEnter(Collider other)
    {
        if (!other.TryGetComponent(out IDamagable target)) return;

        if (targetOverlapRefCount.TryGetValue(target, out var n))
            targetOverlapRefCount[target] = n + 1;
        else
        {
            targetOverlapRefCount[target] = 1;
            targets.Add(target);
        }
    }

    private void OnExit(Collider other)
    {
        if (!other.TryGetComponent(out IDamagable target)) return;
        if (!targetOverlapRefCount.TryGetValue(target, out var n)) return;

        n--;
        if (n <= 0)
        {
            targetOverlapRefCount.Remove(target);
            targets.Remove(target);
        }
        else
            targetOverlapRefCount[target] = n;
    }
    #endregion



    private void DebugLog()
    {
        //Debug.Log($"damage: {pickaxeParam.damage} / attackInterval: {pickaxeParam.attackInterval} / criticalRate: {pickaxeParam.criticalRate} / resourceRate: {pickaxeParam.resourceUpRate} / size: {pickaxeParam.size}");
        //Debug.Log($"gameBaseParam: {GameParamManager.gameBaseParam.pickaxeBase_AttackDamage} / {GameParamManager.gameBaseParam.pickaxeBase_AttackInterval} / {GameParamManager.gameBaseParam.pickaxeBase_CriticalRate} / {GameParamManager.gameBaseParam.pickaxeBase_ResourceUpRate} / {GameParamManager.gameBaseParam.pickaxeBase_Size}");
        //Debug.Log($"RESULT == > damage: {damage} / attackInterval: {attackInterval} / criticalRate: {criticalRate} / resourceRate: {resourceUpRate_pickaxe} / size: {size}");
    }
}
