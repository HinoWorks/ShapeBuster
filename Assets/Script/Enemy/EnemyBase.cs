using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 敵の共通処理。体力と、死亡時に EnemyGenerateManager へ返す。
/// 移動は Rigidbody2D の速度。当たり判定用の Collider2D は同じ GameObject に付ける。
/// Wall タグのコライダーに当たると、接触法線で進行方向を反射する。
/// 反射で変わったY速度は、出現時のY速度へ徐々に戻す。
/// </summary>
public class EnemyBase : MonoBehaviour, IDamagable
{
    [SerializeField] int maxHp = 1;
    [SerializeField] EnemyHpFill hpFill;

    EnemyGenerateManager owner;
    EnemyData enemyData;
    Rigidbody2D body;
    bool inPool;
    const float SpecifiedVelocityYRecoverTime = 1f;
    const float SpawnScaleDuration = 0.25f;

    float moveSpeed;
    float rotateSpeed;
    float specifiedVelocityY;
    bool spawnedInFormation;
    Vector2 moveDirection = Vector2.down;
    Transform damageVisual;
    Vector3 damageVisualScale = Vector3.one;
    Vector3 spawnScale = Vector3.one;
    bool damageVisualScaleCaptured;
    Action<EnemyBase> defeatedListener;


    public bool isAlive { get; private set; }
    public bool IsBoss { get; private set; }
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

    /// <summary>false のとき、出現時のY速度へ戻さない。移動パターン側が速度を持つ。</summary>
    protected virtual bool RecoverVelocityYEnabled => true;

    protected bool SpawnedInFormation => spawnedInFormation;
    protected float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    protected void SetMoveDirection(Vector2 direction)
    {
        moveDirection = direction.sqrMagnitude <= 0.0001f ? Vector2.down : direction.normalized;
        specifiedVelocityY = moveDirection.y * moveSpeed;
    }

    protected void SetVelocity(Vector2 velocity)
    {
        float magnitude = velocity.magnitude;
        if (magnitude <= 0.0001f)
        {
            moveSpeed = 0f;
            return;
        }

        moveSpeed = magnitude;
        moveDirection = velocity / magnitude;
    }

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
        spawnScale = transform.localScale;
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

    public void MarkAsBoss()
    {
        IsBoss = true;
    }

    public void Activate(Vector3 worldPosition, EnemyData enemyData, bool fallAsFormation = false)
    {
        inPool = false;
        defeatedListener = null;
        IsBoss = false;
        this.enemyData = enemyData;
        rotateSpeed = enemyData.rotateSpeed * randomRate_rotateSpeed;
        DamageAmount = enemyData.damage;
        spawnedInFormation = fallAsFormation;
        if (fallAsFormation)
        {
            moveSpeed = enemyData.speed;
            moveDirection = Vector2.down;
        }
        else
        {
            moveSpeed = enemyData.speed * randomRate_speed;
            float angle = randomRate_goAngle;
            moveDirection = Quaternion.Euler(0f, 0f, angle) * Vector3.down;
        }
        specifiedVelocityY = moveDirection.y * moveSpeed;
        MaxHp = Mathf.Max(1, enemyData.maxHp);
        CurrentHp = MaxHp;

        transform.position = worldPosition;
        OnSpawn();
        RefreshHpFill();
        CacheDamageVisual();

        gameObject.SetActive(true);
        ScaleAnim();

        body.linearVelocity = moveDirection * moveSpeed;
        body.angularVelocity = rotateSpeed;

        isAlive = true;
    }

    void FixedUpdate()
    {
        if (!isAlive) return;

        TickMovement();
        if (RecoverVelocityYEnabled)
            RecoverVelocityY();
        body.linearVelocity = moveDirection * moveSpeed;
        body.angularVelocity = rotateSpeed;
    }

    void ScaleAnim()
    {
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(spawnScale, SpawnScaleDuration).SetEase(Ease.OutBounce);
    }

    /// <summary>速度を決めたあと、毎物理フレームで移動を上書きする。</summary>
    protected virtual void TickMovement() { }

    /// <summary>
    /// Y速度が出現時の値でなければ、その差が |出現時のY速度| と同じとき 1 秒で届く速さで近づける。
    /// 横方向の速度はそのまま残す。
    /// </summary>
    void RecoverVelocityY()
    {
        Vector2 velocity = moveDirection * moveSpeed;
        if (Mathf.Abs(specifiedVelocityY - velocity.y) <= 0.0001f) return;

        float step = Mathf.Abs(specifiedVelocityY) / SpecifiedVelocityYRecoverTime * Time.fixedDeltaTime;
        velocity.y = Mathf.MoveTowards(velocity.y, specifiedVelocityY, step);

        float magnitude = velocity.magnitude;
        if (magnitude <= 0.0001f) return;

        moveSpeed = magnitude;
        moveDirection = velocity / magnitude;
    }

    public void MarkInPool()
    {
        inPool = true;
        isAlive = false;
    }

    public bool Damage(int damage, float resourceUpRate = 1f, int attackIndex = -1, int enemiesInArea = 1)
    {
        if (!isAlive) return true;

        damage = SkillTreeBonus.ScaleDealtDamage(damage, enemiesInArea, IsBoss);
        int applied = Mathf.Max(0, ModifyDamage(damage, resourceUpRate));
        CurrentHp -= applied;
        if (applied > 0)
            GameEvent.InGame.PublishDamageDealt(applied, attackIndex);
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
        transform.localScale = spawnScale;
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

    /// <summary>壁で進行方向を反射した直後。normal は壁から敵へ向く。</summary>
    protected virtual void OnWallReflected(Vector2 normal) { }
    protected virtual void OnDamaged(int appliedDamage, float resourceUpRate) { }


    protected virtual void OnDeath(float resourceUpRate)
    {
        if (EffectManager.Inst != null)
            EffectManager.Inst.Rent(EffectType.EnemyDead, transform.position);

        CameraManager.Inst.Shake(CameraShakeLevel.Small);
        GameEvent.InGame.PublishEnemyDefeated(enemyData != null ? enemyData.enemyIndex : -1);
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

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryReflectFromWall(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryReflectFromWall(collision);
    }

    void TryReflectFromWall(Collision2D collision)
    {
        if (!isAlive) return;
        if (!collision.collider.CompareTag("Wall")) return;

        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            if (Vector2.Dot(moveDirection, normal) >= 0f) continue;

            moveDirection = Vector2.Reflect(moveDirection, normal).normalized;
            body.linearVelocity = moveDirection * moveSpeed;
            OnWallReflected(normal);
        }
    }
}
