using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 敵の共通処理。体力と、死亡時に EnemyGenerateManager へ返す。
/// 移動は Rigidbody2D の速度。当たり判定用の Collider2D は同じ GameObject に付ける。
/// </summary>
public class EnemyBase : MonoBehaviour, IDamagable
{
    [SerializeField] int maxHp = 1;
    [SerializeField] EnemyHpFill hpFill;

    EnemyGenerateManager owner;
    EnemyData enemyData;
    Rigidbody2D body;
    bool inPool;
    float moveSpeed;
    float rotateSpeed;
    Vector2 moveDirection = Vector2.down;
    Transform damageVisual;
    Vector3 damageVisualScale = Vector3.one;
    bool damageVisualScaleCaptured;
    Action<EnemyBase> defeatedListener;


    public bool isAlive { get; private set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int DefaultMaxHp => maxHp;
    public int DamageAmount { get; private set; }
    public EnemyBase SourcePrefab { get; private set; }
    public Transform GetTransform() => transform;

    private float randomRate_speed => UnityEngine.Random.Range(0.8f, 1.1f);
    private float randomRate_rotateSpeed => UnityEngine.Random.Range(0.8f, 1.1f);
    private float randomRate_goAngle => UnityEngine.Random.Range(-15f, 15f);

    /// <summary>false のとき、死亡後の Release は派生クラスが呼ぶ。</summary>
    protected virtual bool ReleaseOnDeath => true;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.None;
    }



    public void Bind(EnemyGenerateManager manager, EnemyBase sourcePrefab)
    {
        owner = manager;
        SourcePrefab = sourcePrefab;
    }

    public void SetDefeatedListener(Action<EnemyBase> listener)
    {
        defeatedListener = listener;
    }

    public void Activate(Vector3 worldPosition, EnemyData enemyData)
    {
        inPool = false;
        defeatedListener = null;
        this.enemyData = enemyData;
        moveSpeed = enemyData.speed * randomRate_speed;
        rotateSpeed = enemyData.rotateSpeed * randomRate_rotateSpeed;
        DamageAmount = enemyData.damage;
        float angle = randomRate_goAngle;
        moveDirection = Quaternion.Euler(0f, 0f, angle) * Vector3.down;
        MaxHp = Mathf.Max(1, enemyData.maxHp);
        CurrentHp = MaxHp;

        transform.position = worldPosition;
        OnSpawn();
        RefreshHpFill();
        CacheDamageVisual();

        gameObject.SetActive(true);

        body.linearVelocity = moveDirection * moveSpeed;
        body.angularVelocity = rotateSpeed;

        isAlive = true;
    }

    void FixedUpdate()
    {
        if (!isAlive) return;

        body.linearVelocity = moveDirection * moveSpeed;
        body.angularVelocity = rotateSpeed;
    }

    public void MarkInPool()
    {
        inPool = true;
        isAlive = false;
    }

    public bool Damage(int damage, float resourceUpRate = 1f)
    {
        if (!isAlive) return true;

        int applied = Mathf.Max(0, ModifyDamage(damage, resourceUpRate));
        CurrentHp -= applied;
        if (applied > 0)
            GameEvent.InGame.PublishDamageDealt(applied);
        OnDamaged(applied, resourceUpRate);
        RefreshHpFill();
        DamageAnim();

        if (CurrentHp > 0) return false;

        CurrentHp = 0;
        isAlive = false;
        OnDeath(resourceUpRate);
        if (ReleaseOnDeath)
            Release();
        return true;
    }

    /// <summary>管理者のプールへ戻す。管理者がいなければ破棄する。</summary>
    public void Release()
    {
        if (inPool) return;

        inPool = true;
        isAlive = false;
        OnRelease();
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        transform.DOKill();
        ResetDamageAnim();

        if (owner != null)
            owner.Return(this);
        else
            Destroy(gameObject);
    }

    void RefreshHpFill()
    {
        if (hpFill == null)
            hpFill = GetComponentInChildren<EnemyHpFill>(true);
        if (hpFill == null) return;

        float ratio = MaxHp <= 0 ? 0f : Mathf.Clamp01((float)CurrentHp / MaxHp);
        hpFill.SetRatio(ratio);
    }

    protected virtual void DamageAnim()
    {
        CacheDamageVisual();
        if (damageVisual == null) return;

        damageVisual.DOKill();
        damageVisual.localScale = damageVisualScale;
        DOTween.Sequence()
            .Append(damageVisual.DOScale(damageVisualScale * 1.1f, 0.05f).SetEase(Ease.OutQuad))
            .Append(damageVisual.DOScale(damageVisualScale, 0.07f).SetEase(Ease.InQuad))
            .SetTarget(damageVisual);

        hpFill?.PlayDamageFlash();
    }

    void CacheDamageVisual()
    {
        if (hpFill == null)
            hpFill = GetComponentInChildren<EnemyHpFill>(true);

        var visual = hpFill != null ? hpFill.transform : transform;
        if (damageVisual == visual && damageVisualScaleCaptured) return;

        damageVisual = visual;
        damageVisualScale = visual.localScale;
        damageVisualScaleCaptured = true;
    }

    void ResetDamageAnim()
    {
        if (damageVisual != null)
        {
            damageVisual.DOKill();
            damageVisual.localScale = damageVisualScale;
        }
        hpFill?.RestoreVisual();
    }


    protected virtual int ModifyDamage(int damage, float resourceUpRate) => damage;
    protected virtual void OnSpawn() { }
    protected virtual void OnDamaged(int appliedDamage, float resourceUpRate) { }


    protected virtual void OnDeath(float resourceUpRate)
    {
        CameraManager.Inst.Shake(CameraShakeLevel.Small);
        GameEvent.InGame.PublishEnemyDefeated();
        if (owner != null && enemyData != null)
            owner.SpawnResources(transform.position, enemyData);

        var listener = defeatedListener;
        defeatedListener = null;
        listener?.Invoke(this);
    }
    protected virtual void OnRelease() { }


    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("DeadLine"))
        {
            GameEvent.InGame.PublishHpBarDamaged(DamageAmount);
            Release();
        }
    }
}
