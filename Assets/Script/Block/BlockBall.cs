using System.Collections.Generic;
using UnityEngine;

public enum BlockBallKind
{
    Normal,
    Bomb,
    Laser,
    LaserRow,
    Heavy,
    Orbit,
    Cross,
    Poison,
    Turret = 10,
    AutoBeam = 11
}

/// <summary>
/// 手元にある間は止まっている。放つと一定の速さで跳ね、ブロックに当たるとダメージを与える。
/// ボムは当たったときたまに周囲を巻き込み、縦レーザーはその列、横レーザーはその段を焼く。
/// ヘビーは遅くブロックを貫通する。オービットは周りを回る玉もブロックを削る。
/// クロスレーザーは斜め2本、ポイズンはたまにその場へ沼を残す。
/// </summary>
public class BlockBall : MonoBehaviour
{
    const int OrbitCount = 3;
    const float OrbitRadius = 0.55f;
    const float OrbitHitRadius = 0.1f;
    const float OrbitTurnsPerSecond = 1.35f;
    const float StuckTravel = 0.5f;
    const int StuckFlips = 5;
    const float StuckKick = 0.5f;

    static readonly Collider2D[] orbitHits = new Collider2D[12];
    static ContactFilter2D orbitFilter;
    static bool orbitFilterReady;

    Rigidbody2D body;
    Collider2D hit;
    SpriteRenderer sprite;
    TrailRenderer[] trails;
    readonly List<Collider2D> ignored = new();
    float speed;
    // 壁の帯（加速・減速）で変わった速さ。boostLeft が残っている間だけ効き、終わりに向けて元の速さへ戻る。
    float boostRate = 1f;
    float boostTime;
    float boostLeft;
    Color spriteColor = Color.white;
    Color boostTint = Color.white;
    int damage;
    Vector2 launchDirection = Vector2.up;
    Vector2 approach;
    Vector2 forced;
    bool forceVelocity;
    float sidewaysSign;
    float sidewaysCross;
    int sidewaysFlips;
    bool sidewaysReady;
    float verticalSign;
    float verticalCross;
    int verticalFlips;
    bool verticalReady;
    Transform[] orbits;
    List<BlockPiece>[] orbitTouch;
    float orbitAngle;
    int hitsBefore;
    bool fullStack;
    bool wentAboveLaunch;

    public BlockBallKind Kind { get; private set; }
    public bool FullStack => fullStack;
    public bool WentAboveLaunch => wentAboveLaunch;

    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

    /// <summary>今の速さ。壁の帯の効果を含む。</summary>
    float Speed => speed * BoostScale;

    /// <summary>速さの倍率。効果時間の前半はそのまま、後半で 1 へ戻す。</summary>
    float BoostScale
    {
        get
        {
            if (boostLeft <= 0f || boostTime <= 0f) return 1f;
            float t = Mathf.Clamp01(boostLeft / (boostTime * 0.5f));
            return 1f + (boostRate - 1f) * t;
        }
    }

    public bool SpeedChanged => boostLeft > 0f;

    /// <summary>
    /// 壁の帯に触れた。rate 倍の速さを time 秒（後半は戻りながら）続ける。
    /// 1 より大きいと加速、小さいと減速。もう一度触れたら、あとから触れた帯の効果で数え直す。
    /// </summary>
    public void ChangeSpeed(float rate, float time, Color tint)
    {
        if (IsHeld || body == null || !body.simulated) return;
        boostTint = tint;
        boostRate = Mathf.Clamp(rate, 0.2f, 4f);
        boostTime = Mathf.Max(0.05f, time);
        boostLeft = boostTime;
        if (body.linearVelocity.sqrMagnitude > 0.0001f)
            body.linearVelocity = body.linearVelocity.normalized * Speed;
    }

    void ClearBoost()
    {
        boostLeft = 0f;
        TintBoost();
    }

    /// <summary>効果が残っている間、ボールを帯の色（boostTint）に寄せる。</summary>
    void TintBoost()
    {
        if (sprite == null) return;
        float span = boostRate - 1f;
        float k = Mathf.Abs(span) > 0.001f ? (BoostScale - 1f) / span : 0f;
        var target = new Color(boostTint.r, boostTint.g, boostTint.b, spriteColor.a);
        sprite.color = Color.Lerp(spriteColor, target, k * 0.8f);
    }

    public void NoteAboveLaunch()
    {
        wentAboveLaunch = true;
    }

    public void SetFullStack(bool value)
    {
        fullStack = value;
    }
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

    public void Init(PhysicsMaterial2D material, float ballSpeed, BlockBallKind kind)
    {
        Kind = kind;
        speed = Mathf.Max(0.1f, ballSpeed);
        damage = 1;
        trails = GetComponentsInChildren<TrailRenderer>(true);
        CutTrail();

        sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (sprite == null)
            Debug.LogWarning("ボールの子に SpriteRenderer がありません。", this);
        else if (sprite.sprite == null)
            sprite.sprite = BlockSession.CircleSprite;
        if (sprite != null)
            spriteColor = sprite.color;

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
        CreateOrbits();
    }

    public void SetVisible(bool visible)
    {
        if (sprite != null)
            sprite.enabled = visible;
        if (!visible)
            CutTrail();
        ShowOrbits(visible && !IsHeld);
    }

    public void Hold(Vector2 position)
    {
        IsHeld = true;
        wentAboveLaunch = false;
        forceVelocity = false;
        ClearIgnored();
        CutTrail();
        transform.position = new Vector3(position.x, position.y, 0f);
        if (body == null) return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.position = position;
        body.simulated = false;
        ShowOrbits(false);
        ClearStuck();
    }

    public void Fly(Vector2 direction)
    {
        if (body == null) return;

        IsHeld = false;
        wentAboveLaunch = false;
        forceVelocity = false;
        ClearIgnored();
        SetVisible(true);
        hitsBefore = 0;
        ClearBoost();
        launchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        approach = launchDirection * speed;
        body.simulated = true;
        body.position = transform.position;
        body.linearVelocity = approach;
        BeginTrail();
        ClearStuck();
    }

    public void Stop()
    {
        ClearBoost();
        CutTrail();
        ShowOrbits(false);
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
        if (boostLeft > 0f)
        {
            boostLeft -= Time.fixedDeltaTime;
            TintBoost();
        }
        bool pierced = forceVelocity;
        if (forceVelocity)
        {
            body.linearVelocity = forced;
            forceVelocity = false;
        }
        KeepSpeed(launchDirection);
        if (!pierced)
            WatchStuck();
        approach = body.linearVelocity;
    }

    void LateUpdate()
    {
        TickOrbits();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsHeld) return;

        var piece = collision.collider.GetComponent<BlockPiece>();
        if (piece != null)
        {
            Vector2 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : (Vector2)transform.position;
            bool boss = piece.BossIndex >= 0;
            var direct = SkillTreeBonus.RollHit(Kind, hitsBefore, boss);
            damage = direct.damage;
            if (!piece.HitFrom(damage, hitPoint, (int)Kind, direct.critical, fullStack))
            {
                KeepSpeed(launchDirection);
                return;
            }

            hitsBefore++;
            var splash = SkillTreeBonus.RollHit(Kind, 0, boss);
            int area = splash.damage;
            float proc = SkillTreeBonus.ProcChance(Kind);
            if (Kind == BlockBallKind.Bomb && Random.value < proc)
                BlockSession.Inst?.Explode(piece, area, splash.critical, fullStack);
            else if (Kind == BlockBallKind.Laser && Random.value < proc)
                BlockSession.Inst?.Beam(piece, area, splash.critical, fullStack);
            else if (Kind == BlockBallKind.LaserRow && Random.value < proc)
                BlockSession.Inst?.BeamRow(piece, area, splash.critical, fullStack);
            else if (Kind == BlockBallKind.Cross && Random.value < proc)
                BlockSession.Inst?.BeamCross(piece, area, splash.critical, fullStack);
            else if (Kind == BlockBallKind.Poison && Random.value < proc)
                BlockSession.Inst?.Poison(piece, damage, fullStack);

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

        Vector2 velocity = approach.sqrMagnitude > 0.01f ? approach : launchDirection * Speed;
        forced = velocity.normalized * Speed;
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

        float minAxis = Speed * 0.05f;
        if (Mathf.Abs(velocity.x) < minAxis)
            velocity.x = Mathf.Sign(velocity.x == 0f ? fallback.x : velocity.x) * minAxis;
        if (Mathf.Abs(velocity.y) < minAxis)
            velocity.y = Mathf.Sign(velocity.y == 0f ? 1f : velocity.y) * minAxis;

        body.linearVelocity = velocity.normalized * Speed;
    }

    /// <summary>
    /// 同じ向きの反転を繰り返しても、直交する方向がほとんど動かないときスタックとみなす。
    /// 横なら縦成分を、縦なら横成分を足して往復から抜ける。
    /// </summary>
    void WatchStuck()
    {
        if (body == null) return;

        Vector2 velocity = body.linearVelocity;
        Vector2 position = body.position;
        WatchAxis(ref sidewaysSign, ref sidewaysCross, ref sidewaysFlips, ref sidewaysReady,
            velocity.x, position.y, true);
        velocity = body.linearVelocity;
        WatchAxis(ref verticalSign, ref verticalCross, ref verticalFlips, ref verticalReady,
            velocity.y, position.x, false);
    }

    void WatchAxis(ref float sign, ref float cross, ref int flips, ref bool ready,
        float axisVelocity, float crossPosition, bool kickVertical)
    {
        if (Mathf.Abs(axisVelocity) < 0.01f) return;

        float nextSign = axisVelocity > 0f ? 1f : -1f;
        if (!ready)
        {
            ready = true;
            sign = nextSign;
            cross = crossPosition;
            flips = 0;
            return;
        }

        if (nextSign == sign) return;

        float travel = Mathf.Abs(crossPosition - cross);
        sign = nextSign;
        cross = crossPosition;
        if (travel >= StuckTravel)
        {
            flips = 0;
            return;
        }

        flips++;
        if (flips < StuckFlips) return;

        Vector2 velocity = body.linearVelocity;
        int axis = kickVertical ? 1 : 0;
        float kickSign = Mathf.Abs(velocity[axis]) < 0.01f ? 1f : Mathf.Sign(velocity[axis]);
        velocity[axis] = kickSign * Mathf.Max(Mathf.Abs(velocity[axis]), Speed * StuckKick);
        body.linearVelocity = velocity.normalized * Speed;
        flips = 0;
    }

    void ClearStuck()
    {
        sidewaysReady = false;
        sidewaysFlips = 0;
        verticalReady = false;
        verticalFlips = 0;
    }

    void CreateOrbits()
    {
        if (Kind != BlockBallKind.Orbit) return;

        EnsureOrbitFilter();
        var found = new List<Transform>(OrbitCount);
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.name == "Orbit")
                found.Add(child);
        }

        int wanted = Mathf.Max(OrbitCount, SkillTreeBonus.OrbitCount);
        while (found.Count < wanted)
            found.Add(CreateOrbitDot().transform);
        if (found.Count > wanted)
            found.RemoveRange(wanted, found.Count - wanted);

        orbits = found.ToArray();
        orbitTouch = new List<BlockPiece>[orbits.Length];
        for (int i = 0; i < orbits.Length; i++)
        {
            orbits[i].gameObject.SetActive(false);
            orbitTouch[i] = new List<BlockPiece>(4);
        }
    }

    GameObject CreateOrbitDot()
    {
        var host = new GameObject("Orbit");
        host.layer = BlockSession.BallLayer;
        host.transform.SetParent(transform, false);
        host.transform.localScale = Vector3.one * 0.22f;
        var dot = host.AddComponent<SpriteRenderer>();
        dot.sprite = BlockSession.CircleSprite;
        dot.color = new Color(1f, 0.93f, 0.55f, 1f);
        dot.sortingOrder = 6;
        return host;
    }

    static void EnsureOrbitFilter()
    {
        if (orbitFilterReady) return;
        orbitFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        orbitFilter.SetLayerMask(1 << BlockSession.BlockLayer);
        orbitFilterReady = true;
    }

    void ShowOrbits(bool visible)
    {
        if (orbits == null) return;
        for (int i = 0; i < orbits.Length; i++)
        {
            if (orbits[i] != null)
                orbits[i].gameObject.SetActive(visible);
            if (!visible && orbitTouch != null)
                orbitTouch[i].Clear();
        }
    }

    void TickOrbits()
    {
        if (orbits == null) return;

        bool flying = !IsHeld && body != null && body.simulated;
        ShowOrbits(flying);
        if (!flying) return;

        orbitAngle += Time.deltaTime * OrbitTurnsPerSecond * Mathf.PI * 2f;
        float step = Mathf.PI * 2f / orbits.Length;
        for (int i = 0; i < orbits.Length; i++)
        {
            float angle = orbitAngle + step * i;
            Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OrbitRadius;
            orbits[i].position = pos;
            SweepOrbit(i, pos);
        }
    }

    void SweepOrbit(int index, Vector2 pos)
    {
        var touching = orbitTouch[index];
        int count = Physics2D.OverlapCircle(pos, OrbitHitRadius, orbitFilter, orbitHits);
        for (int i = touching.Count - 1; i >= 0; i--)
        {
            var piece = touching[i];
            if (piece == null || !piece.IsAlive || !Overlaps(piece, count))
                touching.RemoveAt(i);
        }

        for (int i = 0; i < count; i++)
        {
            var piece = orbitHits[i] != null ? orbitHits[i].GetComponent<BlockPiece>() : null;
            if (piece == null || !piece.IsAlive || touching.Contains(piece)) continue;
            touching.Add(piece);
            var roll = SkillTreeBonus.RollHit(Kind, 0, piece.BossIndex >= 0);
            piece.HitFrom(roll.damage, pos, (int)Kind, roll.critical, fullStack);
        }
    }

    static bool Overlaps(BlockPiece piece, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (orbitHits[i] != null && orbitHits[i].GetComponent<BlockPiece>() == piece)
                return true;
        }
        return false;
    }

    static void ApplyLayer(Transform target)
    {
        target.gameObject.layer = BlockSession.BallLayer;
        for (int i = 0; i < target.childCount; i++)
            ApplyLayer(target.GetChild(i));
    }
}
