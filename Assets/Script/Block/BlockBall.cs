using System.Collections.Generic;
using UnityEngine;

public enum BlockBallKind
{
    Normal,
    Bomb,
    Laser,
    Heavy
}

/// <summary>
/// 手元にある間は止まっている。放つと一定の速さで跳ね、ブロックに当たるとダメージを与える。
/// ボムとレーザーは当たったときたまに範囲を巻き込み、ヘビーは遅くブロックを貫通する。
/// </summary>
public class BlockBall : MonoBehaviour
{
    const float ProcChance = 0.3f;

    Rigidbody2D body;
    Collider2D hit;
    SpriteRenderer sprite;
    TrailRenderer[] trails;
    readonly List<Collider2D> ignored = new();
    float speed;
    int damage;
    Vector2 launchDirection = Vector2.up;
    Vector2 approach;
    Vector2 forced;
    bool forceVelocity;

    public BlockBallKind Kind { get; private set; }
    public bool IsHeld { get; private set; }

    public float Bottom
    {
        get
        {
            if (hit != null)
                return hit.bounds.min.y;
            return transform.position.y - BlockSession.BallDiameter * 0.5f;
        }
    }

    public void Init(PhysicsMaterial2D material, int ballDamage, float ballSpeed, BlockBallKind kind)
    {
        Kind = kind;
        speed = Mathf.Max(0.1f, ballSpeed);
        damage = Mathf.Max(1, ballDamage);
        trails = GetComponentsInChildren<TrailRenderer>(true);
        CutTrail();

        sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (sprite == null)
            Debug.LogWarning("ボールの子に SpriteRenderer がありません。", this);
        else if (sprite.sprite == null)
            sprite.sprite = BlockSession.CircleSprite;

        hit = GetComponentInChildren<Collider2D>(true);
        if (hit == null)
        {
            var created = gameObject.AddComponent<CircleCollider2D>();
            created.radius = 0.5f;
            hit = created;
        }
        hit.sharedMaterial = material;
        ApplyLayer(transform);

        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.sharedMaterial = material;
        gameObject.layer = BlockSession.BallLayer;
        IsHeld = true;
        body.simulated = false;
    }

    public void SetVisible(bool visible)
    {
        if (sprite != null)
            sprite.enabled = visible;
        if (!visible)
            CutTrail();
    }

    public void Hold(Vector2 position)
    {
        IsHeld = true;
        forceVelocity = false;
        ClearIgnored();
        CutTrail();
        transform.position = new Vector3(position.x, position.y, 0f);
        if (body == null) return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = position;
        body.simulated = false;
    }

    public void Fly(Vector2 direction)
    {
        if (body == null) return;

        IsHeld = false;
        forceVelocity = false;
        ClearIgnored();
        SetVisible(true);
        launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        approach = launchDirection * speed;
        body.simulated = true;
        body.position = transform.position;
        body.linearVelocity = approach;
        BeginTrail();
    }

    public void Stop()
    {
        CutTrail();
        if (body == null) return;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
    }

    void CutTrail()
    {
        if (trails == null) return;
        for (int i = 0; i < trails.Length; i++)
        {
            var trail = trails[i];
            if (trail == null) continue;
            trail.emitting = false;
            trail.Clear();
            trail.enabled = false;
        }
    }

    void BeginTrail()
    {
        if (trails == null) return;
        for (int i = 0; i < trails.Length; i++)
        {
            var trail = trails[i];
            if (trail == null) continue;
            trail.enabled = true;
            trail.Clear();
            trail.emitting = true;
        }
    }

    void FixedUpdate()
    {
        if (body == null || !body.simulated || IsHeld) return;
        if (forceVelocity)
        {
            body.linearVelocity = forced;
            forceVelocity = false;
        }
        KeepSpeed(launchDirection);
        approach = body.linearVelocity;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsHeld) return;

        var piece = collision.collider.GetComponent<BlockPiece>();
        if (piece != null)
        {
            Vector2 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)transform.position;
            piece.Hit(damage);
            if (Kind == BlockBallKind.Bomb && Random.value < ProcChance)
                BlockSession.Inst?.Explode(point);
            else if (Kind == BlockBallKind.Laser && Random.value < ProcChance)
                BlockSession.Inst?.Beam(point.x);

            if (Kind == BlockBallKind.Heavy)
            {
                Pierce(collision.collider);
                return;
            }
        }

        KeepSpeed(launchDirection);
    }

    void Pierce(Collider2D block)
    {
        if (hit != null && block != null)
        {
            Physics2D.IgnoreCollision(hit, block, true);
            ignored.Add(block);
        }

        Vector2 velocity = approach.sqrMagnitude > 0.01f ? approach : launchDirection * speed;
        forced = velocity.normalized * speed;
        forceVelocity = true;
        if (body == null) return;
        body.linearVelocity = forced;
        body.position += forced.normalized * 0.05f;
    }

    void ClearIgnored()
    {
        for (int i = 0; i < ignored.Count; i++)
        {
            var other = ignored[i];
            if (other != null && hit != null)
                Physics2D.IgnoreCollision(hit, other, false);
        }
        ignored.Clear();
    }

    void KeepSpeed(Vector2 fallback)
    {
        if (body == null) return;

        Vector2 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude < 0.01f)
            velocity = fallback.sqrMagnitude > 0.0001f ? fallback : Vector2.up;

        float minAxis = speed * 0.05f;
        if (Mathf.Abs(velocity.x) < minAxis)
            velocity.x = Mathf.Sign(velocity.x == 0f ? fallback.x : velocity.x) * minAxis;
        if (Mathf.Abs(velocity.y) < minAxis)
            velocity.y = Mathf.Sign(velocity.y == 0f ? 1f : velocity.y) * minAxis;

        body.linearVelocity = velocity.normalized * speed;
    }

    static void ApplyLayer(Transform target)
    {
        target.gameObject.layer = BlockSession.BallLayer;
        for (int i = 0; i < target.childCount; i++)
            ApplyLayer(target.GetChild(i));
    }
}
