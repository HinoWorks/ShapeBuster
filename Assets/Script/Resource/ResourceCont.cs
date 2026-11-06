using UnityEngine;
using DG.Tweening;

/// <summary>
/// 敵を倒したときに出るリソース。生成とプールは EnemyGenerateManager が行う。
/// </summary>
[DisallowMultipleComponent]
public class ResourceCont : MonoBehaviour
{
    EnemyGenerateManager owner;
    Rigidbody2D body;
    bool inPool;
    float fallSpeed = 1f;
    public bool isCollecting { get; private set; } = false;

    public ResourceType ResourceType { get; private set; }
    public ResourceCont SourcePrefab { get; private set; }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Bind(EnemyGenerateManager manager, ResourceCont sourcePrefab)
    {
        owner = manager;
        SourcePrefab = sourcePrefab;
    }

    public void Activate(Vector3 worldPosition, ResourceType resourceType, float speed)
    {
        inPool = false;
        isCollecting = false;
        ResourceType = resourceType;
        fallSpeed = Mathf.Max(0.01f, speed);
        gameObject.SetActive(true);
        transform.DOKill();
        transform.localScale = Vector3.one;
        transform.position = worldPosition;
        if (body != null)
        {
            body.simulated = true;
            body.position = worldPosition;
            body.linearVelocity = Vector2.down * fallSpeed;
        }
    }

    void Update()
    {
        if (inPool || isCollecting || !PointerWorld.IsInsidePlayfield) return;

        float range = 0.4f * (1f + Mathf.Max(0f, SkillTreeBonus.PickupRange));
        Vector2 delta = (Vector2)PointerWorld.Position - (Vector2)transform.position;
        if (delta.sqrMagnitude > range * range) return;
        Collect();
    }

    void FixedUpdate()
    {
        if (inPool || isCollecting || body == null) return;

        body.linearVelocity = Vector2.down * fallSpeed;
        if (body.position.y <= DeadlineTop())
            Release();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (inPool || isCollecting) return;
        if (!other.CompareTag("DeadLine")) return;
        Release();
    }

    public void MarkInPool()
    {
        inPool = true;
    }

    /// <summary>1個獲得してプールへ戻す。プール済みなら何もしない。</summary>
    public void Collect()
    {
        if (inPool || isCollecting) return;

        isCollecting = true;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        var start = transform.position;
        var cursor = CursorPoint(start.z);
        var awayDir = start - cursor;
        if (awayDir.sqrMagnitude < 0.0001f)
            awayDir = Vector3.up;
        else
            awayDir.Normalize();
        var away = start + awayDir * 0.45f;
        away.z = start.z;

        var pullFrom = away;
        DOTween.Sequence()
            .SetTarget(transform)
            .Append(transform.DOMove(away, 0.1f).SetEase(Ease.OutCubic))
            .AppendCallback(() => pullFrom = transform.position)
            .Append(DOTween.To(() => 0f, rate =>
            {
                transform.position = Vector3.LerpUnclamped(pullFrom, CursorPoint(pullFrom.z), rate);
            }, 1f, 0.18f).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                if (inPool) return;
                GameEvent.UI.PublishResourceMod_Ingame(ResourceType, 1);
                Release();
            });
    }

    static Vector3 CursorPoint(float z)
    {
        Vector3 point = PointerWorld.Position;
        if (!PointerWorld.IsInsidePlayfield)
        {
            var cursor = AttackManager.Inst != null ? AttackManager.Inst.CursorTransform : null;
            if (cursor != null)
                point = cursor.position;
        }

        point.z = z;
        return point;
    }

    /// <summary>管理者のプールへ戻す。管理者がいなければ破棄する。</summary>
    public void Release()
    {
        if (inPool) return;

        inPool = true;
        isCollecting = false;
        transform.DOKill();
        transform.localScale = Vector3.one;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        if (owner != null)
            owner.Return(this);
        else
            Destroy(gameObject);
    }

    static float deadlineTop = float.NaN;

    static float DeadlineTop()
    {
        if (!float.IsNaN(deadlineTop)) return deadlineTop;

        var colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            if (col == null || !col.CompareTag("DeadLine")) continue;
            Vector3 center = col.transform.TransformPoint(col.offset);
            float height = Mathf.Abs(col.transform.lossyScale.y) * col.bounds.size.y;
            if (col is BoxCollider2D box)
                height = Mathf.Abs(col.transform.lossyScale.y) * box.size.y;
            deadlineTop = center.y + height * 0.5f;
            return deadlineTop;
        }

        deadlineTop = -4.75f;
        return deadlineTop;
    }
}
