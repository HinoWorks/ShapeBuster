using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UniRx;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class AttackCont_Cursor : MonoBehaviour
{
    protected CursorParam cursorParam;

    protected bool isActive = false;//　Init後、攻撃開始タイミング同期用。trueになったら攻撃開始

    public int Damage => damage;
    protected int damage => MilestoneBonus.ScaleCursorDamage(Mathf.Max(1, 1 + SkillTreeBonus.CursorDamage));
    protected float attackInterval => Mathf.Max(0.05f, cursorParam.attackInterval + MilestoneBonus.AttackIntervalAdd);
    protected float criticalRate => cursorParam.criticalRate + MilestoneBonus.CriticalRate;
    protected float resourceUpRate_pickaxe => cursorParam.resourceUpRate + 0f;
    protected float size => Mathf.Max(0.01f, cursorParam.size + MilestoneBonus.CursorSizeAdd);
    float CurrentReloadDuration => Mathf.Max(0.05f, reloadDuration * MilestoneBonus.ReloadScale);
    int magazineCapacity;
    float reloadDuration;


    [SerializeField] GameObject obj_pointerArea;
    [SerializeField] TriggerSender[] triggerSender;
    [SerializeField] ParticleSystem eff_critical;
    [SerializeField] TMP_Text bulletCount;
    [SerializeField] TMP_Text tmp_onOff;
    [SerializeField] SpriteRenderer timeBar;
    [SerializeField] GameObject tmp_reload;
    [SerializeField] Color reloadBarColor = Color.red;

    Vector3 timeBarBaseScale;
    float timeBarBaseX;
    float timeBarPivotX = 0.5f;
    Color timeBarBaseColor = Color.white;
    bool timeBarCached;

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

    int ammoInMag;
    int spareMagazines;
    bool isStopped;
    bool isReloading;
    bool isDisposed;
    float fireTimer;
    float reloadTimer;

    public int AmmoInMagazine => ammoInMag;
    public int MagazineCapacity => magazineCapacity;
    public int TotalBullets => spareMagazines * magazineCapacity + (isReloading ? magazineCapacity : ammoInMag);
    public bool IsFireStopped => isStopped;
    public bool IsReloading => isReloading;



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
        magazineCapacity = Mathf.Max(1, cursorParam.magazineAmmo);
        spareMagazines = Mathf.Max(0, Mathf.Max(1, cursorParam.magazineCount) - 1);
        reloadDuration = cursorParam.reloadTime > 0f ? cursorParam.reloadTime : 1.2f;
        ammoInMag = magazineCapacity;
        isStopped = false;
        isReloading = false;
        fireTimer = attackInterval;
        reloadTimer = 0f;
        CacheTimeBar();
        //CreateAttackRoop();
        this.gameObject.SetActive(false);
        RefreshBulletHud();
        RefreshCursorVisual();

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
        if (isDisposed) return;
        isDisposed = true;

        Destroy(this.gameObject);
    }

    void Update()
    {
        if (cursorParam == null) return;
        RefreshBulletHud();
        if (isActive)
        {
            ReadGunInput();
            TickGun();
        }
        RefreshCursorVisual();
    }

    void CacheTimeBar()
    {
        if (timeBar == null || timeBarCached) return;

        timeBarBaseScale = timeBar.transform.localScale;
        timeBarBaseX = timeBar.transform.localPosition.x;
        timeBarBaseColor = timeBar.color;
        if (timeBar.sprite != null && timeBar.sprite.rect.width > 0f)
            timeBarPivotX = timeBar.sprite.pivot.x / timeBar.sprite.rect.width;
        timeBarCached = true;
    }

    void RefreshCursorVisual()
    {
        if (bulletCount != null)
        {
            string ammoText = ammoInMag.ToString();
            if (bulletCount.text != ammoText)
                bulletCount.text = ammoText;
        }

        if (tmp_onOff != null)
        {
            string stateText = isStopped ? "OFF" : "ON";
            if (tmp_onOff.text != stateText)
                tmp_onOff.text = stateText;
        }

        if (tmp_reload != null && tmp_reload.activeSelf != isReloading)
            tmp_reload.SetActive(isReloading);

        if (timeBar == null) return;
        CacheTimeBar();

        float fill = 0f;
        Color color = timeBarBaseColor;
        if (isReloading)
        {
            color = reloadBarColor;
            fill = CurrentReloadDuration <= 0f ? 0f : 1f - Mathf.Clamp01(reloadTimer / CurrentReloadDuration);
        }
        else if (ammoInMag > 0)
        {
            fill = attackInterval <= 0f ? 1f : Mathf.Clamp01(fireTimer / attackInterval);
        }

        float width = timeBarBaseScale.x * fill;
        var scale = timeBarBaseScale;
        scale.x = width;
        timeBar.transform.localScale = scale;

        var position = timeBar.transform.localPosition;
        float left = timeBarBaseX - timeBarBaseScale.x * timeBarPivotX;
        position.x = left + width * timeBarPivotX;
        timeBar.transform.localPosition = position;
        timeBar.color = color;
    }

    void ReadGunInput()
    {
        var mouse = Mouse.current;
        if (mouse == null || IsOverBlockingUI()) return;

        if (mouse.leftButton.wasPressedThisFrame)
            isStopped = !isStopped;

        if (mouse.rightButton.wasPressedThisFrame)
            TryStartReload();
    }

    void TickGun()
    {
        if (isReloading)
        {
            reloadTimer += Time.deltaTime;
            if (reloadTimer >= CurrentReloadDuration)
                FinishReload();
        }

        if (isReloading || isStopped || ammoInMag <= 0 || !PointerWorld.IsInsidePlayfield) return;

        fireTimer += Time.deltaTime;
        while (fireTimer >= attackInterval && ammoInMag > 0 && !isReloading)
        {
            fireTimer -= attackInterval;
            Fire();
        }
    }

    void Fire()
    {
        if (ammoInMag <= 0) return;
        ammoInMag--;
        Attack();
        if (ammoInMag <= 0)
            TryStartReload();
    }

    void TryStartReload()
    {
        if (isReloading || spareMagazines <= 0 || ammoInMag >= magazineCapacity) return;

        spareMagazines--;
        ammoInMag = 0;
        isReloading = true;
        reloadTimer = 0f;
        GameEvent.InGame.PublishReloadStarted();
    }

    void FinishReload()
    {
        isReloading = false;
        ammoInMag = magazineCapacity;
        reloadTimer = 0f;
        fireTimer = attackInterval;
    }

    static bool IsOverBlockingUI()
    {
        if (EventSystem.current == null) return false;
        var pointer = Pointer.current;
        if (pointer == null) return false;
        return EventSystem.current.IsPointerOverGameObject(pointer.device.deviceId);
    }

    void RefreshBulletHud()
    {
        if (UIManager_Ingame.Inst.BulletInfo == null) return;
        UIManager_Ingame.Inst.BulletInfo.SetBullet(ammoInMag, magazineCapacity, TotalBullets);
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

    private void Attack()
    {
        AttackManager.Inst.NotifyPickaxeAttackTiming();

        var isCritical = UnityEngine.Random.Range(0f, 1f) < criticalRate;
        calc_criticalDamageRate = isCritical ? criticalDamageRate : 1f;
        if (isCritical)
            GameEvent.InGame.PublishCriticalHit();

        if (isCritical && eff_critical != null)
            eff_critical.Play();

        if (obj_pointerArea != null)
        {
            obj_pointerArea.transform.DOKill();
            obj_pointerArea.transform.localScale = size * Vector3.one;
            obj_pointerArea.transform.DOScale(1.1f * size * Vector3.one, 0.075f).SetEase(Ease.OutBack);
            obj_pointerArea.transform.DOScale(size * Vector3.one, 0.075f).SetEase(Ease.OutBack).SetDelay(0.075f);
        }

        if (ApplyHits() > 0)
            Check_AttackAddTime();
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
        Debug.Log($"damage: {damage} / attackInterval: {attackInterval} / criticalRate: {criticalRate} / resourceRate: {resourceUpRate_pickaxe} / size: {size} / ammo: {magazineCapacity} / total: {TotalBullets} / reload: {CurrentReloadDuration}");
        //Debug.Log($"gameBaseParam: {GameParamManager.gameBaseParam.pickaxeBase_AttackDamage} / {GameParamManager.gameBaseParam.pickaxeBase_AttackInterval} / {GameParamManager.gameBaseParam.pickaxeBase_CriticalRate} / {GameParamManager.gameBaseParam.pickaxeBase_ResourceUpRate} / {GameParamManager.gameBaseParam.pickaxeBase_Size}");
        //Debug.Log($"RESULT == > damage: {damage} / attackInterval: {attackInterval} / criticalRate: {criticalRate} / resourceRate: {resourceUpRate_pickaxe} / size: {size}");
    }
}
