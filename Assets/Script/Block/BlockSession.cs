using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ブロック崩しの1セッション。
/// 10列×6行を配置率70%で出し、全体を滑らかに下降させる。
/// 新しい群の下限は画面上部1/4。ブロックがデッドラインに触れると終了する。
/// ボールは3つ。狙った方向へ、手持ちを0.2秒おきに続けて打つ。下のラインで回収する。
/// 取得済みのボム、レーザー、ヘビーはノーマルのあと、同じ方向へ自動で続く。
/// </summary>
public class BlockSession : MonoBehaviour
{
    public const int Columns = 10;
    public const int Rows = 6;
    public const int BlockLayer = 8;
    public const int BallLayer = 9;
    public const float BallDiameter = 0.32f;

    const float BaseFill = 0.7f;
    const float MaxFill = 0.85f;
    const float FillPerSecond = 0.002f;
    const float BaseSpeed = 0.1f;
    const float SpeedPerSecond = 0.005f;
    const float BaseInterval = 8f;
    const float IntervalShrinkPerSecond = 0.045f;
    const float MinInterval = 4.2f;
    const int BaseHp = 2;
    const float HpPerSecond = 0.1f;
    /// <summary>1マス増えるごとに足すHP倍率。プレハブの hpScale もさらに掛ける。</summary>
    const float SizeHpScale = 1f;
    const int BallCount = 3;
    const float BallSpeed = 13f;
    const float BlockGap = 0.1f;
    const float ShotInterval = 0.2f;
    const int BallDamage = 1;
    const float BlockSizeScale = 0.5f;
    const float MinPull = 0.28f;
    const float GuideScale = 1.6f;
    const float GuideMax = 3.4f;
    const float HeavySpeedScale = 0.5f;
    const float BombRadiusInCells = 2.5f;
    const string NormalBallPath = "Ball/pf_Ball_Normal";
    const string BombBallPath = "Ball/pf_Ball_Bomb";
    const string LaserBallPath = "Ball/pf_Ball_Laser";
    const string HeavyBallPath = "Ball/pf_Ball_Heavy";
    const string LeftWallPath = "Field/pf_FieldWall_Left";
    const string RightWallPath = "Field/pf_FieldWall_Right";
    const string DeadlinePath = "Field/pf_FieldDeadline";
    const float WallThickness = 0.3f;
    /// <summary>カメラの外まで壁を塗る余白。画面シェイクで隙間が出ない量。</summary>
    const float WallOverscan = 2f;

    public static BlockSession Inst { get; private set; }

    /// <summary>インゲームはブロック崩し。敵の湧きとカーソル射撃は使わない。</summary>
    public const bool Enabled = true;

    static Sprite squareSprite;
    static Sprite circleSprite;

    public static Sprite SquareSprite => squareSprite != null ? squareSprite : (squareSprite = MakeSprite(false));
    public static Sprite CircleSprite => circleSprite != null ? circleSprite : (circleSprite = MakeSprite(true));

    readonly List<BlockPiece> pieces = new();
    readonly List<GameObject> arena = new();

    Transform formation;
    Rigidbody2D formationBody;
    BlockBall[] balls;
    BlockBall aimingBall;
    readonly List<BlockBall> shotQueue = new();
    readonly List<BallFlash> flashes = new();
    UI_BlockBallHud ballHud;
    BlockPiece[] blockPrefabs;
    PhysicsMaterial2D bounce;
    Transform pullGuide;
    Transform shotGuide;
    Transform shotTip;
    bool running;
    bool pendingFail;
    bool aiming;
    bool volley;
    float shotTimer;
    Vector2 shotDirection;
    float elapsed;
    float waveTimer;
    float deadlineY;
    float ceilingY;
    float screenBottom;
    float screenTop;
    float spawnFloorY;
    float fieldLeft;
    float fieldRight;
    float cell;
    Vector2 launchAnchor;

    public bool IsRunning => running;
    public float Elapsed => elapsed;

    /// <summary>一番下のブロックがデッドラインにどれだけ近いか。0 が天井側、1 がライン上。</summary>
    public float Danger
    {
        get
        {
            if (!running || pieces.Count == 0) return 0f;

            float lowest = float.PositiveInfinity;
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece == null || !piece.IsAlive) continue;
                if (piece.Bottom < lowest)
                    lowest = piece.Bottom;
            }
            if (float.IsPositiveInfinity(lowest)) return 0f;

            float span = ceilingY - deadlineY;
            if (span <= 0.01f) return 1f;
            return Mathf.Clamp01(1f - (lowest - deadlineY) / span);
        }
    }

    public static void Ensure()
    {
        if (Inst != null) return;
        var host = new GameObject(nameof(BlockSession));
        host.AddComponent<BlockSession>();
    }

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame:
                Begin();
                break;
            case GameStateType.InGame_Ready:
            case GameStateType.InGame_End:
            case GameStateType.Title:
            case GameStateType.OutGame:
                Clear();
                break;
        }
    }

    void Update()
    {
        if (pendingFail)
        {
            pendingFail = false;
            Fail();
            return;
        }

        if (!running) return;

        waveTimer += Time.deltaTime;
        float interval = CurrentInterval;
        if (waveTimer < interval) return;

        waveTimer -= interval;
        SpawnWave();
    }

    void FixedUpdate()
    {
        if (!running || pendingFail || formationBody == null) return;

        elapsed += Time.fixedDeltaTime;
        formationBody.MovePosition(formationBody.position + Vector2.down * (CurrentSpeed * Time.fixedDeltaTime));
        if (ReachedDeadline())
        {
            pendingFail = true;
            running = false;
        }
    }

    public void NotifyBroken(BlockPiece piece)
    {
        pieces.Remove(piece);
    }

    float CurrentSpeed => BaseSpeed + elapsed * SpeedPerSecond;

    float CurrentInterval => Mathf.Max(MinInterval, BaseInterval - elapsed * IntervalShrinkPerSecond);

    float CurrentFill => Mathf.Clamp(BaseFill + elapsed * FillPerSecond, BaseFill, MaxFill);

    int CurrentHp => Mathf.Max(1, Mathf.RoundToInt(BaseHp + elapsed * HpPerSecond));

    void Begin()
    {
        Clear();
        running = true;
        elapsed = 0f;
        waveTimer = 0f;
        BuildArena();
        LoadBlockPrefabs();
        SpawnWave();
        SpawnBalls();
    }

    void Fail()
    {
        running = false;
        pendingFail = false;
        if (balls != null)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null)
                    balls[i].Stop();
            }
        }
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Medium);
        Debug.Log("<color=yellow> ========= Deadline ========= </color>");
        GameWatcher.Inst?.SetGameState(GameStateType.InGame_End);
    }

    void Clear()
    {
        running = false;
        pendingFail = false;
        aiming = false;
        volley = false;
        shotTimer = 0f;
        shotQueue.Clear();
        flashes.Clear();
        elapsed = 0f;
        waveTimer = 0f;
        pieces.Clear();

        if (formation != null)
            Destroy(formation.gameObject);
        formation = null;
        formationBody = null;
        balls = null;
        aimingBall = null;
        ballHud = null;
        pullGuide = null;
        shotGuide = null;
        shotTip = null;
        if (bounce != null)
            Destroy(bounce);
        bounce = null;

        for (int i = 0; i < arena.Count; i++)
        {
            if (arena[i] != null)
                Destroy(arena[i]);
        }
        arena.Clear();
    }

    void BuildArena()
    {
        if (!AttackPlayfield.TryGetRect(out var view))
            view = new Rect(-5f, -5f, 10f, 10f);

        deadlineY = view.yMin + 0.45f;
        ceilingY = view.yMax + WallOverscan;
        screenBottom = view.yMin;
        screenTop = view.yMax;
        spawnFloorY = view.yMax - view.height * 0.25f;

        float midX = (view.xMin + view.xMax) * 0.5f;
        cell = Mathf.Max(0.12f, (view.width / Columns) * BlockSizeScale);
        float gridWidth = cell * Columns;
        fieldLeft = midX - gridWidth * 0.5f - cell;
        fieldRight = midX + gridWidth * 0.5f + cell;
        var root = new GameObject("BlockFormation");
        formation = root.transform;
        root.layer = BlockLayer;
        formation.position = new Vector3(midX, 0f, 0f);
        formationBody = root.AddComponent<Rigidbody2D>();
        formationBody.bodyType = RigidbodyType2D.Kinematic;
        formationBody.gravityScale = 0f;
        formationBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        formationBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        formationBody.useFullKinematicContacts = true;
        formationBody.position = new Vector2(midX, 0f);

        bounce = new PhysicsMaterial2D("BlockBounce")
        {
            bounciness = 1f,
            friction = 0f,
            bounceCombine = PhysicsMaterialCombine2D.Maximum,
            frictionCombine = PhysicsMaterialCombine2D.Minimum
        };

        Physics2D.IgnoreLayerCollision(BlockLayer, 0, true);
        Physics2D.IgnoreLayerCollision(BlockLayer, BlockLayer, true);
        Physics2D.IgnoreLayerCollision(BallLayer, BlockLayer, false);
        Physics2D.IgnoreLayerCollision(BallLayer, 0, false);
        Physics2D.IgnoreLayerCollision(BallLayer, BallLayer, true);

        float outerLeft = view.xMin - WallOverscan;
        float outerRight = view.xMax + WallOverscan;
        float outerBottom = view.yMin - WallOverscan;
        float outerTop = ceilingY + WallThickness;
        PlaceWallFill(LeftWallPath, outerLeft, fieldLeft, outerBottom, outerTop, true);
        PlaceWallFill(RightWallPath, fieldRight, outerRight, outerBottom, outerTop, true);
        CreateWall(
            new Vector2((fieldLeft + fieldRight) * 0.5f, ceilingY + WallThickness * 0.5f),
            new Vector2(fieldRight - fieldLeft, WallThickness),
            false);
        PlaceDeadline(fieldRight - fieldLeft);
        pullGuide = CreateGuide("PullGuide", new Color(1f, 1f, 1f, 0.45f), 6, false);
        shotGuide = CreateGuide("ShotGuide", new Color(1f, 0.92f, 0.45f, 0.95f), 7, false);
        shotTip = CreateGuide("ShotTip", new Color(1f, 0.92f, 0.45f, 0.95f), 7, true);
        CreateBallHud();
    }

    /// <summary>
    /// 壁プレハブで矩形を塗る。跳ね返る面は内側の縁。solid のときだけ当たり判定を残す。
    /// </summary>
    void PlaceWallFill(string path, float xMin, float xMax, float yMin, float yMax, bool solid)
    {
        float width = xMax - xMin;
        float height = yMax - yMin;
        if (width <= 0.001f || height <= 0.001f) return;

        var center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"壁プレハブがありません。 {path}");
            CreateWall(center, new Vector2(width, height), true);
            if (!solid && arena.Count > 0)
            {
                var fallback = arena[arena.Count - 1].GetComponent<BoxCollider2D>();
                if (fallback != null)
                    fallback.enabled = false;
            }
            return;
        }

        var authored = prefab.transform.localScale;
        float xSign = authored.x < 0f ? -1f : 1f;
        float ySign = authored.y < 0f ? -1f : 1f;
        var instance = Instantiate(prefab);
        instance.transform.position = new Vector3(center.x, center.y, prefab.transform.localPosition.z);
        instance.transform.localScale = new Vector3(xSign * width, ySign * height, authored.z == 0f ? 1f : authored.z);

        var colliders = instance.GetComponentsInChildren<BoxCollider2D>(true);
        if (!solid)
        {
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }
        else
        {
            if (colliders.Length == 0)
                colliders = new[] { instance.AddComponent<BoxCollider2D>() };
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].sharedMaterial = bounce;
        }
        arena.Add(instance);
    }

    /// <summary>
    /// デッドラインのプレハブを置く。スケールYが太さ。幅はフィールドに合わせる。
    /// </summary>
    void PlaceDeadline(float width)
    {
        var prefab = Resources.Load<GameObject>(DeadlinePath);
        if (prefab == null)
        {
            Debug.LogWarning($"デッドラインのプレハブがありません。 {DeadlinePath}");
            CreateDeadlineMark(width);
            return;
        }

        var authored = prefab.transform.localScale;
        float thickness = Mathf.Abs(authored.y);
        if (thickness < 0.0001f)
            thickness = 0.06f;
        float ySign = authored.y < 0f ? -1f : 1f;

        var instance = Instantiate(prefab);
        instance.transform.position = new Vector3((fieldLeft + fieldRight) * 0.5f, deadlineY, prefab.transform.localPosition.z);
        instance.transform.localScale = new Vector3(width, ySign * thickness, authored.z == 0f ? 1f : authored.z);
        arena.Add(instance);
    }

    void CreateWall(Vector2 center, Vector2 size, bool visible)
    {
        var wallObject = new GameObject("BlockWall");
        wallObject.transform.position = center;
        wallObject.transform.localScale = new Vector3(size.x, size.y, 1f);
        var collider = wallObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.sharedMaterial = bounce;
        if (visible)
        {
            var sprite = wallObject.AddComponent<SpriteRenderer>();
            sprite.sprite = SquareSprite;
            sprite.color = new Color(0.62f, 0.7f, 0.8f, 1f);
            sprite.sortingOrder = 4;
        }
        arena.Add(wallObject);
    }

    Transform CreateGuide(string name, Color color, int order, bool circle)
    {
        var guide = new GameObject(name);
        var sprite = guide.AddComponent<SpriteRenderer>();
        sprite.sprite = circle ? CircleSprite : SquareSprite;
        sprite.color = color;
        sprite.sortingOrder = order;
        guide.SetActive(false);
        arena.Add(guide);
        return guide.transform;
    }

    void CreateDeadlineMark(float width)
    {
        var mark = new GameObject("Deadline");
        mark.transform.position = new Vector3((fieldLeft + fieldRight) * 0.5f, deadlineY, 0f);
        mark.transform.localScale = new Vector3(width, 0.06f, 1f);
        var sprite = mark.AddComponent<SpriteRenderer>();
        sprite.sprite = SquareSprite;
        sprite.color = new Color(0.95f, 0.28f, 0.25f, 0.9f);
        sprite.sortingOrder = 6;
        arena.Add(mark);
    }

    void SpawnBalls()
    {
        launchAnchor = new Vector2((fieldLeft + fieldRight) * 0.5f, deadlineY + 0.55f);
        var spawned = new List<BlockBall>(BallCount + 3);
        for (int i = 0; i < BallCount; i++)
            AddBall(spawned, BlockBallKind.Normal, NormalBallPath, BallSpeed, true);

        AddBall(spawned, BlockBallKind.Bomb, BombBallPath, BallSpeed, false);
        AddBall(spawned, BlockBallKind.Laser, LaserBallPath, BallSpeed, false);
        AddBall(spawned, BlockBallKind.Heavy, HeavyBallPath, BallSpeed * HeavySpeedScale, false);
        balls = spawned.ToArray();
        if (ballHud != null)
            ballHud.Set(CountLaunchable(), balls.Length);
    }

    void AddBall(List<BlockBall> spawned, BlockBallKind kind, string resourcePath, float speed, bool always)
    {
        if (!always && !Owns(kind)) return;

        var prefab = Resources.Load<BlockBall>(resourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"ボールプレハブがありません。 {resourcePath}");
            return;
        }

        var ball = Instantiate(prefab);
        ball.Init(bounce, BallDamage, speed, kind);
        ball.Hold(launchAnchor);
        ball.SetVisible(false);
        spawned.Add(ball);
        arena.Add(ball.gameObject);
    }

    static bool Owns(BlockBallKind kind)
    {
        if (AttackManager.Inst != null && AttackManager.Inst.OwnsDebugBall(kind))
            return true;
        return SkillTreeBonus.OwnsBall(kind);
    }

    void CreateBallHud()
    {
        var canvas = UIManager_Ingame.Inst != null ? UIManager_Ingame.Inst.HudCanvas : null;
        if (canvas == null) return;

        TMP_FontAsset font = null;
        var sample = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        if (sample != null)
            font = sample.font;

        ballHud = UI_BlockBallHud.Create(canvas.transform, font);
        arena.Add(ballHud.gameObject);
        ballHud.Set(BallCount, BallCount);
    }

    void LateUpdate()
    {
        if (!running || balls == null) return;

        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball == null || ball.IsHeld || ball == aimingBall) continue;
            if (ball.Bottom <= deadlineY)
                Park(ball);
        }

        TickVolley();
        TickAim();
        TickFlashes();
        if (ballHud != null)
            ballHud.Set(CountLaunchable(), balls.Length);
    }

    int CountLaunchable()
    {
        int count = 0;
        if (balls == null) return 0;
        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] != null && balls[i].IsHeld)
                count++;
        }
        return count;
    }

    BlockBall FindReadyBall()
    {
        if (balls == null) return null;
        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball != null && ball.IsHeld && ball.Kind == BlockBallKind.Normal)
                return ball;
        }
        return null;
    }

    void TickAim()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            HideGuides();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && PointerWorld.IsInsidePlayfield && aimingBall == null && !volley)
        {
            var ready = FindReadyBall();
            if (ready == null) return;

            launchAnchor = ClampLaunch((Vector2)PointerWorld.Position);
            ready.Hold(launchAnchor);
            ready.SetVisible(true);
            aimingBall = ready;
            aiming = true;
        }

        if (!aiming || !mouse.leftButton.isPressed)
        {
            if (aiming && mouse.leftButton.wasReleasedThisFrame)
                ReleaseAim();
            return;
        }

        Vector2 pull = (Vector2)PointerWorld.Position - launchAnchor;
        if (pull.sqrMagnitude < 0.0025f)
        {
            HideGuides();
            return;
        }

        Vector2 flight = -pull.normalized;
        float guideLength = Mathf.Min(pull.magnitude * GuideScale, GuideMax);
        ShowBar(pullGuide, launchAnchor, launchAnchor + pull, 0.05f);
        ShowBar(shotGuide, launchAnchor, launchAnchor + flight * guideLength, 0.07f);
        if (shotTip != null)
        {
            shotTip.gameObject.SetActive(true);
            shotTip.position = launchAnchor + flight * guideLength;
            shotTip.localScale = Vector3.one * 0.18f;
        }
    }

    void ReleaseAim()
    {
        aiming = false;
        HideGuides();
        var ball = aimingBall;
        aimingBall = null;
        if (ball == null) return;

        Vector2 pull = (Vector2)PointerWorld.Position - launchAnchor;
        if (pull.magnitude < MinPull)
        {
            ball.SetVisible(false);
            return;
        }

        BeginVolley(ball, -pull);
    }

    void BeginVolley(BlockBall first, Vector2 direction)
    {
        shotQueue.Clear();
        shotDirection = direction;
        shotTimer = 0f;
        if (first != null && first.Kind == BlockBallKind.Normal)
            shotQueue.Add(first);
        EnqueueHeld(BlockBallKind.Normal, first);
        EnqueueHeld(BlockBallKind.Bomb, null);
        EnqueueHeld(BlockBallKind.Laser, null);
        EnqueueHeld(BlockBallKind.Heavy, null);

        volley = shotQueue.Count > 0;
        FireQueued();
    }

    void TickVolley()
    {
        if (!volley || shotQueue.Count == 0) return;

        shotTimer += Time.deltaTime;
        if (shotTimer < ShotInterval) return;

        shotTimer -= ShotInterval;
        FireQueued();
    }

    void FireQueued()
    {
        while (shotQueue.Count > 0)
        {
            var ball = shotQueue[0];
            shotQueue.RemoveAt(0);
            if (ball == null || !ball.IsHeld) continue;

            ball.Hold(launchAnchor);
            ball.Fly(shotDirection);
            volley = shotQueue.Count > 0;
            return;
        }

        volley = false;
    }

    Vector2 ClampLaunch(Vector2 point)
    {
        float pad = BallDiameter * 0.5f + 0.04f;
        point.x = Mathf.Clamp(point.x, fieldLeft + pad, fieldRight - pad);
        point.y = Mathf.Clamp(point.y, deadlineY + pad, ceilingY - pad);
        return point;
    }

    void Park(BlockBall ball)
    {
        if (ball == null) return;
        if (aimingBall == ball)
        {
            aiming = false;
            aimingBall = null;
            HideGuides();
        }
        ball.Hold(new Vector2(launchAnchor.x, deadlineY + 0.55f));
        ball.SetVisible(false);
    }

    void EnqueueHeld(BlockBallKind kind, BlockBall skip)
    {
        if (balls == null) return;
        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball == null || ball == skip || !ball.IsHeld || ball.Kind != kind) continue;
            shotQueue.Add(ball);
        }
    }

    public void Explode(Vector2 point)
    {
        float radius = cell * BombRadiusInCells;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            if (!Within(piece, point, radius)) continue;
            piece.Hit(BallDamage);
        }

        ShowFlash(
            point,
            CircleSprite,
            Vector3.one * 0.2f,
            Vector3.one * radius * 2f,
            new Color(1f, 0.45f, 0.12f, 0.8f),
            0.36f);
    }

    public void Beam(float x)
    {
        float pad = cell * 0.55f;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            if (!InColumn(piece, x, pad)) continue;
            piece.Hit(BallDamage);
        }

        float span = Mathf.Max(0.2f, screenTop - screenBottom);
        float extra = span * 0.25f;
        float bottom = screenBottom - extra;
        float top = screenTop + extra;
        float height = top - bottom;
        float width = Mathf.Max(0.08f, cell * 0.42f);
        var scale = new Vector3(width, height, 1f);
        ShowFlash(
            new Vector3(x, (bottom + top) * 0.5f, 0f),
            SquareSprite,
            scale,
            scale,
            new Color(0.45f, 0.95f, 1f, 0.9f),
            0.32f);
    }

    static bool Within(BlockPiece piece, Vector2 point, float radius)
    {
        Vector3 scale = piece.transform.lossyScale;
        float dx = Mathf.Abs(piece.transform.position.x - point.x) - Mathf.Abs(scale.x) * 0.5f;
        float dy = Mathf.Abs(piece.transform.position.y - point.y) - Mathf.Abs(scale.y) * 0.5f;
        dx = Mathf.Max(0f, dx);
        dy = Mathf.Max(0f, dy);
        return dx * dx + dy * dy <= radius * radius;
    }

    static bool InColumn(BlockPiece piece, float x, float pad)
    {
        float half = Mathf.Abs(piece.transform.lossyScale.x) * 0.5f;
        return Mathf.Abs(piece.transform.position.x - x) <= half + pad;
    }

    void ShowFlash(Vector3 position, Sprite image, Vector3 from, Vector3 to, Color color, float life)
    {
        var host = new GameObject("BallFlash");
        host.transform.position = position;
        host.transform.localScale = from;
        var sprite = host.AddComponent<SpriteRenderer>();
        sprite.sprite = image;
        sprite.color = color;
        sprite.sortingOrder = 8;
        flashes.Add(new BallFlash
        {
            transform = host.transform,
            sprite = sprite,
            life = Mathf.Max(0.05f, life),
            color = color,
            from = from,
            to = to
        });
        arena.Add(host);
    }

    void TickFlashes()
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var flash = flashes[i];
            if (flash.sprite == null)
            {
                flashes.RemoveAt(i);
                continue;
            }

            flash.age += Time.deltaTime;
            float t = Mathf.Clamp01(flash.age / flash.life);
            flash.transform.localScale = Vector3.Lerp(flash.from, flash.to, t);
            var color = flash.color;
            color.a *= 1f - t;
            flash.sprite.color = color;
            if (t < 1f) continue;

            Destroy(flash.sprite.gameObject);
            flashes.RemoveAt(i);
        }
    }

    sealed class BallFlash
    {
        public Transform transform;
        public SpriteRenderer sprite;
        public float age;
        public float life;
        public Color color;
        public Vector3 from;
        public Vector3 to;
    }

    void HideGuides()
    {
        if (pullGuide != null) pullGuide.gameObject.SetActive(false);
        if (shotGuide != null) shotGuide.gameObject.SetActive(false);
        if (shotTip != null) shotTip.gameObject.SetActive(false);
    }

    void ShowBar(Transform bar, Vector2 from, Vector2 to, float thickness)
    {
        if (bar == null) return;

        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.02f)
        {
            bar.gameObject.SetActive(false);
            return;
        }

        bar.gameObject.SetActive(true);
        bar.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0f);
        bar.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        bar.localScale = new Vector3(length, thickness, 1f);
    }

    void SpawnWave()
    {
        if (formation == null) return;

        float gridWidth = cell * Columns;
        float stackedBottom = HighestLocalTop();
        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float worldBottom = spawnFloorY;
        if (!float.IsNegativeInfinity(stackedBottom))
        {
            float above = parentY + stackedBottom + BlockGap * 0.5f;
            worldBottom = Mathf.Max(worldBottom, above);
        }

        float localBottom = worldBottom - parentY;
        float localLeft = -gridWidth * 0.5f;
        float fill = CurrentFill;
        var used = new bool[Columns, Rows];
        bool placed = false;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                if (used[x, y] || Random.value > fill) continue;
                if (TryPlace(used, x, y, localLeft, localBottom, PickPrefab()))
                    placed = true;
            }
        }

        if (!placed)
            TryPlace(used, Columns / 2, 0, localLeft, localBottom, PickPrefab());
    }

    void LoadBlockPrefabs()
    {
        if (blockPrefabs != null && blockPrefabs.Length > 0) return;
        blockPrefabs = Resources.LoadAll<BlockPiece>("Block");
    }

    BlockPiece PickPrefab()
    {
        LoadBlockPrefabs();
        if (blockPrefabs == null || blockPrefabs.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            if (blockPrefabs[i] != null)
                total += blockPrefabs[i].Weight;
        }
        if (total <= 0f) return blockPrefabs[0];

        float roll = Random.value * total;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab == null) continue;
            roll -= prefab.Weight;
            if (roll <= 0f) return prefab;
        }
        return blockPrefabs[blockPrefabs.Length - 1];
    }

    bool TryPlace(bool[,] used, int x, int y, float localLeft, float localBottom, BlockPiece prefab)
    {
        int cellsX = prefab != null ? prefab.CellsX : 1;
        int cellsY = prefab != null ? prefab.CellsY : 1;
        if (!CanOccupy(used, x, y, cellsX, cellsY))
        {
            cellsX = 1;
            cellsY = 1;
            if (!CanOccupy(used, x, y, cellsX, cellsY)) return false;
        }

        Occupy(used, x, y, cellsX, cellsY);
        Place(prefab, x, y, cellsX, cellsY, localLeft, localBottom);
        return true;
    }

    static bool CanOccupy(bool[,] used, int x, int y, int cellsX, int cellsY)
    {
        if (x < 0 || y < 0 || x + cellsX > Columns || y + cellsY > Rows) return false;
        for (int iy = 0; iy < cellsY; iy++)
        {
            for (int ix = 0; ix < cellsX; ix++)
            {
                if (used[x + ix, y + iy]) return false;
            }
        }
        return true;
    }

    static void Occupy(bool[,] used, int x, int y, int cellsX, int cellsY)
    {
        for (int iy = 0; iy < cellsY; iy++)
        {
            for (int ix = 0; ix < cellsX; ix++)
                used[x + ix, y + iy] = true;
        }
    }

    /// <summary>
    /// 経過前のHPを1レベル分にする。増えた分はレベルを上げ、端数はそのレベルの残りHPにする。
    /// 色数を超えた分は、一番上のレベルのHPに足す。
    /// </summary>
    void SplitDurability(int cells, float hpScale, out int level, out int current, out int barMax, out int stepHp)
    {
        float scale = SizeScale(cells, hpScale);
        stepHp = Mathf.Max(1, Mathf.RoundToInt(BaseHp * scale));
        int total = Mathf.Max(stepHp, Mathf.RoundToInt(CurrentHp * scale));

        level = Mathf.Max(1, Mathf.CeilToInt(total / (float)stepHp));
        current = total - (level - 1) * stepHp;
        if (current <= 0)
            current = stepHp;

        int cap = SO_BlockLevel.MaxCount;
        if (level > cap)
        {
            current += (level - cap) * stepHp;
            level = cap;
            barMax = current;
            return;
        }

        barMax = stepHp;
    }

    static float SizeScale(int cells, float hpScale)
    {
        float scale = 1f + (Mathf.Max(1, cells) - 1) * SizeHpScale;
        return scale * Mathf.Max(0f, hpScale);
    }

    void Place(BlockPiece prefab, int x, int y, int cellsX, int cellsY, float localLeft, float localBottom)
    {
        float spanWidth = cell * cellsX;
        float spanHeight = cell * cellsY;
        float width = Mathf.Max(0.02f, spanWidth - BlockGap);
        float height = Mathf.Max(0.02f, spanHeight - BlockGap);
        BlockPiece piece;
        if (prefab != null)
            piece = Instantiate(prefab, formation);
        else
        {
            var host = new GameObject("Block");
            host.transform.SetParent(formation, false);
            piece = host.AddComponent<BlockPiece>();
        }

        piece.transform.localPosition = new Vector3(
            localLeft + x * cell + spanWidth * 0.5f,
            localBottom + y * cell + spanHeight * 0.5f,
            0f);
        piece.transform.localScale = new Vector3(width, height, 1f);

        int cells = cellsX * cellsY;
        float hpScale = prefab != null ? prefab.HpScale : 1f;
        SplitDurability(cells, hpScale, out int level, out int hp, out int barMax, out int stepHp);
        piece.Spawn(this, new Vector2(width, height), level, hp, barMax, stepHp, cellsX, cellsY);
        pieces.Add(piece);
    }

    float HighestLocalTop()
    {
        float top = float.NegativeInfinity;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null) continue;
            float localTop = piece.transform.localPosition.y + piece.transform.localScale.y * 0.5f;
            if (localTop > top)
                top = localTop;
        }
        return top;
    }

    bool ReachedDeadline()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.IsSolid) continue;
            if (piece.Bottom <= deadlineY)
                return true;
        }
        return false;
    }

    static Sprite MakeSprite(bool circle)
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        float radius = (n - 1) * 0.5f;
        var center = new Vector2(radius, radius);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float alpha = 1f;
                if (circle)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    alpha = Mathf.Clamp01((1f - distance) * 10f);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        sprite.name = circle ? "BlockCircle" : "BlockSquare";
        return sprite;
    }
}
