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

    public int Damage => damage;
    protected int damage => Mathf.Max(1, 1 + SkillTreeBonus.CursorDamage);
    protected float attackInterval => cursorParam.attackInterval;
    protected float criticalRate => cursorParam.criticalRate + 0f;
    protected float resourceUpRate_pickaxe => cursorParam.resourceUpRate + 0f;
    protected float size => cursorParam.size;


    [SerializeField] GameObject obj_pointerArea;
    [SerializeField] TriggerSender[] triggerSender;
    [SerializeField] ParticleSystem eff_critical;

    const int OverlapBufferSize = 64;
    readonly Collider2D[] overlapBuffer = new Collider2D[OverlapBufferSize];
    readonly HashSet<IDamagable> hitTargets = new();
    Collider2D[] hitColliders = System.Array.Empty<Collider2D>();
    ContactFilter2D resourceOverlapFilter;

    public Vector3 pickaxePosition => obj_pointerArea.transform.position;
    public Transform CursorTransform => obj_pointerArea.transform;
    //private Vector3 offsetPosition = new Vector3(0, 0.1f, 0);
    private float calc_criticalDamageRate = 1f;
    private float criticalDamageRate = 2f;

    private readonly SerialDisposable attackLoopDisposable = new SerialDisposable();

    private float timer = 0;
    private CancellationTokenSource CTS;



    protected void Awake()
    {
        attackLoopDisposable.AddTo(this);
        CacheHitColliders();
        resourceOverlapFilter = ContactFilter2D.noFilter;
        resourceOverlapFilter.SetLayerMask(LayerMask.GetMask("Resource"));
    }
    public void Init(CursorParam _cursorParam)
    {
        cursorParam = _cursorParam;
        obj_pointerArea.transform.localScale = size * Vector3.one;
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
    void LateUpdate()
    {
        if (obj_pointerArea == null) return;

        bool show = PointerWorld.IsInsidePlayfield;
        if (obj_pointerArea.activeSelf != show)
            obj_pointerArea.SetActive(show);

        obj_pointerArea.transform.position = PointerWorld.Position;
        CollectResources();
    }

    private async void Attack()
    {
        AttackManager.Inst.NotifyPickaxeAttackTiming();

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
        obj_pointerArea.transform.DOScale(size * Vector3.one, 0.075f).SetEase(Ease.OutBack).SetDelay(0.075f);

        if (ApplyHits() > 0)
        {
            Check_AttackAddTime();
        }

    }

    private void Check_AttackAddTime()
    {
        //if (GameParamManager.gameBaseParam.isPickaxeAttack_AddIngameTime)
        //{
        //    InGameManager.Inst.AddGetExTime(1f);
        //}
    }






    // カーソルは瞬間移動するので、入退場ではなく攻撃時点の重なりを使う
    void CacheHitColliders()
    {
        if (triggerSender == null || triggerSender.Length == 0)
        {
            hitColliders = System.Array.Empty<Collider2D>();
            return;
        }

        var found = new List<Collider2D>(triggerSender.Length);
        foreach (var sender in triggerSender)
        {
            if (sender == null) continue;
            if (sender.TryGetComponent(out Collider2D hitCollider))
                found.Add(hitCollider);
        }
        hitColliders = found.ToArray();
    }

    int ApplyHits()
    {
        if (obj_pointerArea == null || !obj_pointerArea.activeSelf) return 0;

        CollectOverlaps();
        if (hitTargets.Count == 0) return 0;

        int damageCalc = (int)(damage * calc_criticalDamageRate);
        int hitCount = 0;
        foreach (var target in hitTargets)
        {
            if (!target.isAlive) continue;
            target.Damage(damageCalc, resourceUpRate_pickaxe);
            hitCount++;
        }
        return hitCount;
    }

    /// <summary>
    /// リソースを取得
    /// </summary>
    void CollectResources()
    {
        if (obj_pointerArea == null || !obj_pointerArea.activeSelf) return;

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider == null || !hitCollider.enabled) continue;

            int count = hitCollider.Overlap(resourceOverlapFilter, overlapBuffer);
            for (int i = 0; i < count; i++)
            {

                var other = overlapBuffer[i];
                if (other == null) continue;
                if (!other.TryGetComponent(out ResourceCont resource)) continue;
                if (resource.isCollecting) continue;
                resource.Collect();
            }
        }
    }

    /// <summary>
    /// 攻撃対象を取得
    /// </summary>
    void CollectOverlaps()
    {
        hitTargets.Clear();
        var filter = ContactFilter2D.noFilter;
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider == null || !hitCollider.enabled) continue;

            int count = hitCollider.Overlap(filter, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                var other = overlapBuffer[i];
                if (other == null) continue;
                if (!other.TryGetComponent(out IDamagable target)) continue;
                if (!target.isAlive) continue;
                hitTargets.Add(target);
            }
        }
    }



    private void DebugLog()
    {
        Debug.Log($"damage: {damage} / attackInterval: {attackInterval} / criticalRate: {criticalRate} / resourceRate: {resourceUpRate_pickaxe} / size: {size}");
        //Debug.Log($"gameBaseParam: {GameParamManager.gameBaseParam.pickaxeBase_AttackDamage} / {GameParamManager.gameBaseParam.pickaxeBase_AttackInterval} / {GameParamManager.gameBaseParam.pickaxeBase_CriticalRate} / {GameParamManager.gameBaseParam.pickaxeBase_ResourceUpRate} / {GameParamManager.gameBaseParam.pickaxeBase_Size}");
        //Debug.Log($"RESULT == > damage: {damage} / attackInterval: {attackInterval} / criticalRate: {criticalRate} / resourceRate: {resourceUpRate_pickaxe} / size: {size}");
    }
}
