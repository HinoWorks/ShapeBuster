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
    float linger;
    public bool isCollecting { get; private set; } = false;

    // 弾け飛び（宝箱など）。物理を止めて、時間から位置を決める。終わったら普段の落下へ戻る。
    Vector3 burstFrom;
    Vector3 burstTo;
    float burstArc;
    float burstDuration;
    float burstTime;
    float burstDrift;
    /// <summary>弾け飛んでいる最中。この間は拾えない。</summary>
    public bool IsBursting => burstDuration > 0f;

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
        linger = 0f;
        burstDuration = 0f;
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

    /// <summary>
    /// 今の位置から to へ弧を描いて飛ぶ。arc は弧の高さ（ワールド単位）。
    /// 飛んでいる間もフィールドと一緒に下へ流れるので、着地点も同じだけ下がる。
    /// </summary>
    public void Burst(Vector3 to, float duration, float arc)
    {
        if (inPool) return;
        burstFrom = transform.position;
        burstTo = new Vector3(to.x, to.y, burstFrom.z);
        burstArc = arc;
        burstDuration = Mathf.Max(0.01f, duration);
        burstTime = 0f;
        burstDrift = 0f;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
    }

    void TickBurst()
    {
        burstTime += Time.deltaTime;
        burstDrift += fallSpeed * Time.deltaTime;
        float t = Mathf.Clamp01(burstTime / burstDuration);
        // 横は勢いよく飛び出して減速し、縦は山なりに上がってから着地点へ下りる。
        float move = 1f - (1f - t) * (1f - t) * (1f - t);
        var pos = Vector3.LerpUnclamped(burstFrom, burstTo, move);
        pos.y += burstArc * 4f * t * (1f - t) - burstDrift;
        transform.position = pos;
        if (t < 1f) return;

        burstDuration = 0f;
        if (body != null)
        {
            body.simulated = true;
            body.position = pos;
            body.linearVelocity = Vector2.down * fallSpeed;
        }
    }

    void Update()
    {
        if (inPool || isCollecting) return;
        if (IsBursting)
        {
            TickBurst();
            return;
        }
        if (!PointerWorld.IsInsidePlayfield) return;

        var player = SO_Player.Current;
        float range = player.pickupRange * (1f + SkillTreeBonus.BlockMagnetRate + MilestoneBonus.PickupRange);
        Vector2 delta = (Vector2)PointerWorld.Position - (Vector2)transform.position;
        if (delta.sqrMagnitude > range * range) return;
        Collect();
    }

    void FixedUpdate()
    {
        if (inPool || isCollecting || IsBursting || body == null) return;

        var session = BlockSession.Inst;
        if (session != null)
            fallSpeed = Mathf.Max(0.01f, session.BlockFallSpeed);

        body.linearVelocity = Vector2.down * fallSpeed;
        if (body.position.y > DeadlineTop()) return;

        if (SkillTreeBonus.BlockLinger && linger <= 0f)
            linger = 0.4f;
        if (linger > 0f)
        {
            linger -= Time.fixedDeltaTime;
            body.linearVelocity = Vector2.zero;
            if (linger > 0f) return;
        }
        Release();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (inPool || isCollecting) return;
        if (!other.CompareTag("DeadLine")) return;
        if (SkillTreeBonus.BlockLinger && linger <= 0f)
            linger = 0.4f;
        if (linger > 0f) return;
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
        burstDuration = 0f;
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
        var player = SO_Player.Current;
        float kickDistance = player.collectKickDistance;
        float kickDuration = player.collectKickDuration;
        float pullDuration = player.collectPullDuration;
        var away = start + awayDir * kickDistance;
        away.z = start.z;

        var pullFrom = away;
        DOTween.Sequence()
            .SetTarget(transform)
            .Append(transform.DOMove(away, kickDuration).SetEase(Ease.OutCubic))
            .AppendCallback(() => pullFrom = transform.position)
            .Append(DOTween.To(() => 0f, rate =>
            {
                transform.position = Vector3.LerpUnclamped(pullFrom, CursorPoint(pullFrom.z), rate);
            }, 1f, pullDuration).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                if (inPool) return;
                SoundManager.SE(SEType.ResourceCollect);
                GameEvent.UI.PublishResourceMod_Ingame(ResourceType, 1);
                Release();
            });
    }

    static Vector3 CursorPoint(float z)
    {
        Vector3 point = PointerWorld.Position;
        point.z = z;
        return point;
    }

    /// <summary>管理者のプールへ戻す。管理者がいなければ破棄する。</summary>
    public void Release()
    {
        if (inPool) return;

        inPool = true;
        isCollecting = false;
        burstDuration = 0f;
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
