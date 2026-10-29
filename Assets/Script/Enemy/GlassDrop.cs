using DG.Tweening;
using UnityEngine;

/// <summary>
/// 上から落ちるガラス。攻撃が当たると割れ、中のリソースが出る。
/// 出たリソースはカーソルが触れると獲得する。ラインまで割らずに届くと中身は消える。
/// </summary>
[DisallowMultipleComponent]
public class GlassDrop : MonoBehaviour, IDamagable
{
    [SerializeField] SpriteRenderer glassRenderer;
    [SerializeField] SpriteRenderer itemRenderer;
    [SerializeField] float fallSpeed = 0.7f;

    EnemyGenerateManager owner;
    GlassDrop sourcePrefab;
    Rigidbody2D body;
    Collider2D hitCollider;
    ResourceType contents;
    bool inPool;

    public bool isAlive { get; private set; }
    public GlassDrop SourcePrefab => sourcePrefab;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<Collider2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    public void Bind(EnemyGenerateManager manager, GlassDrop prefab)
    {
        owner = manager;
        sourcePrefab = prefab;
    }

    public void Activate(Vector3 worldPosition, ResourceType resourceType, Sprite itemSprite)
    {
        inPool = false;
        isAlive = true;
        contents = resourceType;
        transform.DOKill();
        transform.position = worldPosition;
        transform.localScale = Vector3.one;

        if (glassRenderer != null)
        {
            glassRenderer.DOKill();
            var color = glassRenderer.color;
            color.a = 0.38f;
            glassRenderer.color = color;
            glassRenderer.enabled = true;
        }
        if (itemRenderer != null)
        {
            itemRenderer.sprite = itemSprite;
            itemRenderer.enabled = itemSprite != null;
        }
        if (hitCollider != null)
            hitCollider.enabled = true;
        if (body != null)
            body.linearVelocity = Vector2.down * fallSpeed;
        if (transform.position.y < -8f)
            Release();

        gameObject.SetActive(true);
    }

    public void MarkInPool()
    {
        inPool = true;
        isAlive = false;
    }

    void FixedUpdate()
    {
        if (!isAlive || body == null) return;
        body.linearVelocity = Vector2.down * fallSpeed;
    }

    public bool Damage(int damage, float resourceUpRate = 1f, int attackIndex = -1, int enemiesInArea = 1)
    {
        if (!isAlive || damage <= 0) return false;
        Break();
        return false;
    }

    public Transform GetTransform() => transform;

    void Break()
    {
        isAlive = false;
        if (hitCollider != null)
            hitCollider.enabled = false;
        if (body != null)
            body.linearVelocity = Vector2.zero;
        if (itemRenderer != null)
            itemRenderer.enabled = false;
        if (glassRenderer != null)
        {
            glassRenderer.transform.DOKill();
            glassRenderer.transform.DOPunchScale(Vector3.one * 0.18f, 0.08f, 1, 0.4f);
        }

        owner?.SpawnLooseResource(transform.position, contents);
        Release();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAlive) return;
        if (!other.CompareTag("DeadLine")) return;
        Release();
    }

    public void Release()
    {
        if (inPool) return;

        inPool = true;
        isAlive = false;
        transform.DOKill();
        if (glassRenderer != null)
            glassRenderer.transform.DOKill();
        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (owner != null)
            owner.Return(this);
        else
            Destroy(gameObject);
    }
}
