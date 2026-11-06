using System.Collections.Generic;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ブロック崩しの1セッション。
/// 配置率、HP、下降速度、ボール数などの数値は SO_BlockSession。HPと速度はステージの倍率も掛ける。
/// 未クリアのステージは、stageTime の次の生成でボスを画面外に出す。
/// 新しい群の下限は画面上部1/4。ブロックがデッドラインに触れると終了する。
/// 狙った方向へ、手持ちを戻ってきた順に続けて打つ。下のラインで回収する。
/// </summary>
public class BlockSession : MonoBehaviour
{
    public const int Rows = 6;
    public const int BlockLayer = 8;
    public const int BallLayer = 9;
    public const float BallDiameter = 0.32f;

    public static BlockSession Inst { get; private set; }

    SO_BlockSession rule;
    float blockHpRate = 1f;
    float blockSpeedRate = 1f;

    SO_BlockSession Rule
    {
        get
        {
            if (rule != null) return rule;
            rule = SOLoader.BlockSession;
            if (rule != null) return rule;

            Debug.LogWarning("SO_BlockSession がありません。初期値で進めます。");
            rule = ScriptableObject.CreateInstance<SO_BlockSession>();
            return rule;
        }
    }

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
    readonly List<BlockBall> handOrder = new();
    readonly List<BallFlash> flashes = new();
    readonly List<PoisonZone> poisons = new();
    readonly List<Vector2> aimPath = new();
    UI_BlockBallHud ballHud;
    UI_BlockCursorInfo cursorInfo;
    RectTransform cursorBase;
    BlockPiece[] blockPrefabs;
    BlockPiece[] bossPrefabs;
    bool bossArmed;
    bool bossDue;
    bool bossSpawned;
    bool bossMissingLogged;
    float bossTime;
    int bossIndex;
    PhysicsMaterial2D bounce;
    BlockAimGuide aimGuide;
    bool running;
    bool pendingFail;
    bool aiming;
    bool volley;
    bool pinCursorUi;
    Vector2 pinnedScreen;
    float shotTimer;
    Vector2 shotDirection;
    float elapsed;
    float waveTimer;
    float deadlineY;
    float ceilingY;
    float screenLeft;
    float screenRight;
    float screenBottom;
    float screenTop;
    float spawnFloorY;
    float fieldLeft;
    float fieldRight;
    float cell;
    int columns;
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
        TickBoss();
        if (waveTimer >= interval)
        {
            waveTimer -= interval;
            SpawnWave();
        }
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

    /// <summary>壊した位置へ資源を落とす。カーソルが触れるまで獲得にならない。</summary>
    public void DropResources(Vector3 point, int count)
    {
        var manager = EnemyGenerateManager.Inst;
        if (manager == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            var offset = (Vector3)(Random.insideUnitCircle * 0.2f);
            manager.SpawnLooseResource(point + offset, ResourceType.AA, CurrentSpeed);
        }
    }

    float CurrentSpeed => (Rule.baseSpeed + elapsed * Rule.speedPerSecond) * blockSpeedRate;

    float CurrentInterval => Mathf.Max(Rule.minInterval, Rule.baseInterval - elapsed * Rule.intervalShrinkPerSecond);

    float CurrentFill
    {
        get
        {
            float fill = Mathf.Clamp(Rule.baseFill + elapsed * Rule.fillPerSecond, Rule.baseFill, Rule.maxFill);
            if (bossSpawned)
                fill *= Rule.bossFillScale;
            return fill;
        }
    }

    int CurrentHp => Mathf.Max(1, Mathf.RoundToInt((Rule.baseHp + elapsed * Rule.hpPerSecond) * blockHpRate));

    void Begin()
    {
        Clear();
        ResolveStageRates();
        running = true;
        elapsed = 0f;
        waveTimer = 0f;
        BuildArena();
        LoadBlockPrefabs();
        ArmBoss();
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
        pinCursorUi = false;
        shotTimer = 0f;
        shotQueue.Clear();
        handOrder.Clear();
        flashes.Clear();
        poisons.Clear();
        elapsed = 0f;
        waveTimer = 0f;
        bossArmed = false;
        bossDue = false;
        bossSpawned = false;
        bossMissingLogged = false;
        bossTime = 0f;
        bossIndex = -1;
        pieces.Clear();

        if (formation != null)
            Destroy(formation.gameObject);
        formation = null;
        formationBody = null;
        balls = null;
        aimingBall = null;
        ballHud = null;
        cursorInfo = null;
        cursorBase = null;
        aimGuide = null;
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
        ceilingY = view.yMax;
        screenLeft = view.xMin;
        screenRight = view.xMax;
        screenBottom = view.yMin;
        screenTop = view.yMax;
        spawnFloorY = view.yMax - view.height * 0.25f;

        int baseColumns = ResolveBaseColumns();
        float midX = (view.xMin + view.xMax) * 0.5f;
        float preferred = view.width / Rule.defaultColumns * Rule.blockSizeScale;
        float fitted = view.width / baseColumns;
        float baseCell = Mathf.Max(0.12f, Mathf.Min(preferred, fitted));
        columns = SkillTreeBonus.ColumnsFor(baseColumns);
        cell = Mathf.Max(0.08f, baseCell * baseColumns / columns);
        float blockLeft = midX - cell * columns * 0.5f;
        float blockRight = midX + cell * columns * 0.5f;
        float edge = Rule.blockGap * 0.5f;
        fieldLeft = blockLeft + edge;
        fieldRight = blockRight - edge;
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

        float outerLeft = view.xMin - Rule.wallOverscan;
        float outerRight = view.xMax + Rule.wallOverscan;
        float outerBottom = view.yMin - Rule.wallOverscan;
        float outerTop = view.yMax + Rule.wallOverscan;
        var props = SOLoader.BlockSession;
        PlaceWallFill(props != null ? props.leftWall : null, outerLeft, fieldLeft, outerBottom, outerTop, true);
        PlaceWallFill(props != null ? props.rightWall : null, fieldRight, outerRight, outerBottom, outerTop, true);
        CreateWall(
            new Vector2((fieldLeft + fieldRight) * 0.5f, ceilingY + Rule.wallThickness * 0.5f),
            new Vector2(fieldRight - fieldLeft, Rule.wallThickness),
            false);
        PlaceDeadline(fieldRight - fieldLeft);
        CreateAimGuide();
        CreateBallHud();
        CreateCursorBase();
        CreateCursorInfo();
    }

    void ResolveStageRates()
    {
        blockHpRate = 1f;
        blockSpeedRate = 1f;
        var time = StageTimeManager.Inst;
        var table = SOLoader.StageData;
        if (time == null || table == null) return;

        var stage = table.FindStage(time.StageIndex);
        if (stage == null) return;
        if (stage.blockHpRate > 0f)
            blockHpRate = stage.blockHpRate;
        if (stage.blockSpeedRate > 0f)
            blockSpeedRate = stage.blockSpeedRate;
    }

    int ResolveBaseColumns()
    {
        var time = StageTimeManager.Inst;
        var table = SOLoader.StageData;
        if (time == null || table == null) return Rule.defaultColumns;

        var stage = table.FindStage(time.StageIndex);
        if (stage == null || stage.blockColumns <= 0) return Rule.defaultColumns;
        return stage.blockColumns;
    }

    /// <summary>
    /// 壁プレハブで矩形を塗る。跳ね返る面は内側の縁。solid のときだけ当たり判定を残す。
    /// </summary>
    void PlaceWallFill(GameObject prefab, float xMin, float xMax, float yMin, float yMax, bool solid)
    {
        float width = xMax - xMin;
        float height = yMax - yMin;
        if (width <= 0.001f || height <= 0.001f) return;

        var center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        if (prefab == null)
        {
            Debug.LogWarning("壁プレハブがありません。 SO_BlockSession を確認してください。");
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
        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.deadline : null;
        if (prefab == null)
        {
            Debug.LogWarning("デッドラインのプレハブがありません。 SO_BlockSession を確認してください。");
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

    void CreateAimGuide()
    {
        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.aimGuide : null;
        if (prefab == null)
        {
            Debug.LogWarning("発射方向UIのプレハブがありません。 SO_BlockSession を確認してください。");
            return;
        }

        aimGuide = Instantiate(prefab);
        aimGuide.Hide();
        arena.Add(aimGuide.gameObject);
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
        var props = SOLoader.BlockSession;
        var spawned = new List<BlockBall>(Rule.ballCount + 3);
        for (int i = 0; i < Rule.ballCount; i++)
            AddBall(spawned, BlockBallKind.Normal, props != null ? props.normalBall : null, Rule.ballSpeed, true);

        AddBall(spawned, BlockBallKind.Bomb, props != null ? props.bombBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.Laser, props != null ? props.laserBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.LaserRow, props != null ? props.laserRowBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.Orbit, props != null ? props.orbitBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.Cross, props != null ? props.crossBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.Poison, props != null ? props.poisonBall : null, Rule.ballSpeed, false);
        AddBall(spawned, BlockBallKind.Heavy, props != null ? props.heavyBall : null, Rule.ballSpeed * Rule.heavySpeedScale, false);
        balls = spawned.ToArray();
        handOrder.Clear();
        handOrder.AddRange(spawned);
        if (ballHud != null)
            ballHud.Set(CountLaunchable(), balls.Length);
    }

    void AddBall(List<BlockBall> spawned, BlockBallKind kind, BlockBall prefab, float speed, bool always)
    {
        if (!always && !Owns(kind)) return;

        if (prefab == null)
        {
            Debug.LogWarning($"ボールプレハブがありません。 {kind}");
            return;
        }

        var ball = Instantiate(prefab);
        ball.Init(bounce, Rule.ballDamage, speed, kind);
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
        ballHud.Set(Rule.ballCount, Rule.ballCount);
    }

    void CreateCursorInfo()
    {
        var canvas = UIManager_Ingame.Inst != null ? UIManager_Ingame.Inst.DynamicHudCanvas : null;
        if (canvas == null) return;

        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.cursorInfo : null;
        if (prefab == null)
        {
            Debug.LogWarning("追従UIのプレハブがありません。 SO_BlockSession を確認してください。");
            return;
        }

        cursorInfo = Instantiate(prefab, canvas.transform);
        cursorInfo.Hide();
        arena.Add(cursorInfo.gameObject);
    }

    void CreateCursorBase()
    {
        var canvas = UIManager_Ingame.Inst != null ? UIManager_Ingame.Inst.DynamicHudCanvas : null;
        if (canvas == null) return;

        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.cursorBase : null;
        if (prefab == null)
        {
            Debug.LogWarning("カーソル目印のプレハブがありません。 SO_BlockSession を確認してください。");
            return;
        }

        cursorBase = Instantiate(prefab, canvas.transform);
        cursorBase.SetAsFirstSibling();
        arena.Add(cursorBase.gameObject);
    }

    void TickCursorBase()
    {
        if (cursorBase == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        var canvas = cursorBase.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        var parent = cursorBase.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, mouse.position.ReadValue(), cam, out var local))
            cursorBase.anchoredPosition = local;
    }

    void TickCursorInfo()
    {
        if (cursorInfo == null) return;

        int total = balls != null ? balls.Length : 0;
        var mouse = Mouse.current;
        if (!pinCursorUi && mouse == null)
        {
            cursorInfo.Hide();
            return;
        }

        var screen = pinCursorUi || mouse == null
            ? pinnedScreen
            : mouse.position.ReadValue();
        bool blocked = !pinCursorUi && BlockUnderPointer() != null;
        cursorInfo.Show(CountLaunchable(), total, screen, blocked);
    }

    BlockPiece BlockUnderPointer()
    {
        if (!PointerWorld.IsInsidePlayfield) return null;

        var hit = Physics2D.OverlapPoint((Vector2)PointerWorld.Position, 1 << BlockLayer);
        if (hit == null) return null;
        var piece = hit.GetComponentInParent<BlockPiece>();
        if (piece == null || !piece.IsAlive) return null;
        return piece;
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
        TickCursorBase();
        TickCursorInfo();
        TickFlashes();
        TickPoisons();
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
        for (int i = 0; i < handOrder.Count; i++)
        {
            var ball = handOrder[i];
            if (ball != null && ball.IsHeld)
                return ball;
        }
        return null;
    }

    void TickAim()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            aimGuide?.Hide();
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame && PointerWorld.IsInsidePlayfield && aimingBall == null && !volley && BlockUnderPointer() == null)
        {
            var ready = FindReadyBall();
            if (ready == null) return;

            launchAnchor = ClampLaunch((Vector2)PointerWorld.Position);
            pinnedScreen = mouse.position.ReadValue();
            pinCursorUi = true;
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
            aimGuide?.Hide();
            return;
        }

        if (aimGuide == null) return;

        BuildAimPath(-pull.normalized);
        aimGuide.Show(launchAnchor, pull, aimPath);
    }

    /// <summary>
    /// 発射点から、壁とブロックで跳ねる予測をデッドラインまで積む。
    /// </summary>
    void BuildAimPath(Vector2 direction)
    {
        aimPath.Clear();
        float radius = BallRadius(aimingBall);
        bool pierce = aimingBall != null && aimingBall.Kind == BlockBallKind.Heavy;
        int mask = (1 << 0) | (1 << BlockLayer);
        Vector2 pos = launchAnchor;
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        float left = aimGuide.PredictionLength;
        float floorY = deadlineY + radius;
        aimPath.Add(pos);

        for (int leg = 0; leg < Rule.aimBounces && left > 0.05f; leg++)
        {
            if (pos.y <= floorY)
                return;

            float floorDistance = float.PositiveInfinity;
            if (dir.y < -0.0001f)
                floorDistance = (pos.y - floorY) / -dir.y;

            var hit = Physics2D.CircleCast(pos, radius, dir, left, mask);
            float hitDistance = hit.collider != null ? hit.distance : float.PositiveInfinity;
            if (floorDistance <= hitDistance)
            {
                aimPath.Add(pos + dir * Mathf.Min(floorDistance, left));
                return;
            }

            if (hit.collider == null)
            {
                aimPath.Add(pos + dir * left);
                return;
            }

            float step = Mathf.Max(hit.distance, 0f);
            pos += dir * step;
            left -= step;
            aimPath.Add(pos);

            if (pierce && hit.collider.gameObject.layer == BlockLayer)
            {
                float skip = PierceDistance(hit.collider, dir, radius);
                pos += dir * skip;
                left -= skip;
                continue;
            }

            if (hit.normal.sqrMagnitude < 0.0001f)
                return;

            dir = Vector2.Reflect(dir, hit.normal).normalized;
            pos += hit.normal * Rule.aimSkin;
        }
    }

    float PierceDistance(Collider2D collider, Vector2 direction, float radius)
    {
        var extents = collider.bounds.extents;
        float span = Mathf.Abs(extents.x * direction.x) + Mathf.Abs(extents.y * direction.y);
        return span * 2f + radius + Rule.aimSkin;
    }

    static float BallRadius(BlockBall ball)
    {
        if (ball == null) return BallDiameter * 0.5f;

        var circle = ball.GetComponentInChildren<CircleCollider2D>();
        if (circle == null) return BallDiameter * 0.5f;
        return circle.radius * Mathf.Abs(circle.transform.lossyScale.x);
    }

    void ReleaseAim()
    {
        aiming = false;
        aimGuide?.Hide();
        var ball = aimingBall;
        aimingBall = null;
        if (ball == null) return;

        Vector2 pull = (Vector2)PointerWorld.Position - launchAnchor;
        if (pull.magnitude < Rule.minPull)
        {
            ball.SetVisible(false);
            pinCursorUi = false;
            return;
        }

        BeginVolley(ball, -pull);
    }

    void BeginVolley(BlockBall first, Vector2 direction)
    {
        shotQueue.Clear();
        shotDirection = direction;
        shotTimer = 0f;
        for (int i = 0; i < handOrder.Count; i++)
        {
            var ball = handOrder[i];
            if (ball != null && ball.IsHeld)
                shotQueue.Add(ball);
        }
        if (shotQueue.Count == 0 && first != null && first.IsHeld)
            shotQueue.Add(first);

        volley = shotQueue.Count > 0;
        if (!volley)
            pinCursorUi = false;
        FireQueued();
    }

    void TickVolley()
    {
        if (!volley || shotQueue.Count == 0) return;

        shotTimer += Time.deltaTime;
        if (shotTimer < Rule.shotInterval) return;

        shotTimer -= Rule.shotInterval;
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
            if (!volley)
                pinCursorUi = false;
            return;
        }

        volley = false;
        pinCursorUi = false;
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
            aimGuide?.Hide();
        }
        ball.Hold(new Vector2(launchAnchor.x, deadlineY + 0.55f));
        ball.SetVisible(false);
        handOrder.Remove(ball);
        handOrder.Add(ball);
    }

    /// <summary>ボールスキルの発生位置。当たったブロックの中心を使う。</summary>
    public static Vector2 SkillOrigin(BlockPiece piece)
    {
        return piece != null ? piece.Center : Vector2.zero;
    }

    public void Explode(BlockPiece source)
    {
        Vector2 point = SkillOrigin(source);
        float radius = cell * Rule.bombRadiusInCells;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!Within(piece, point, radius)) continue;
            piece.Hit(Rule.ballDamage);
        }

        ShowFlash(
            point,
            CircleSprite,
            Vector3.one * 0.2f,
            Vector3.one * radius * 2f,
            new Color(1f, 0.45f, 0.12f, 0.8f),
            0.36f);
    }

    public void Beam(BlockPiece source)
    {
        float x = SkillOrigin(source).x;
        float pad = cell * 0.55f;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!InColumn(piece, x, pad)) continue;
            piece.Hit(Rule.ballDamage);
        }

        ShowBeam(new Vector2(x, (screenBottom + screenTop) * 0.5f), Mathf.Max(0.2f, screenTop - screenBottom), false);
    }

    public void BeamRow(BlockPiece source)
    {
        float y = SkillOrigin(source).y;
        float pad = cell * 0.55f;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!InRow(piece, y, pad)) continue;
            piece.Hit(Rule.ballDamage);
        }

        ShowBeam(new Vector2((screenLeft + screenRight) * 0.5f, y), Mathf.Max(0.2f, screenRight - screenLeft), true);
    }

    public void BeamCross(BlockPiece source)
    {
        Vector2 origin = SkillOrigin(source);
        float pad = cell * 0.55f;
        var upRight = new Vector2(0.70710678f, 0.70710678f);
        var downRight = new Vector2(0.70710678f, -0.70710678f);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!OnDiagonal(piece, origin, upRight, pad) && !OnDiagonal(piece, origin, downRight, pad)) continue;
            piece.Hit(Rule.ballDamage);
        }

        float spanX = screenRight - screenLeft;
        float spanY = screenTop - screenBottom;
        float length = Mathf.Sqrt(spanX * spanX + spanY * spanY) * 2.5f;
        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.laserBeamCross : null;
        ShowBeam(origin, length, 45f, prefab);
        ShowBeam(origin, length, -45f, prefab);
    }

    public void Poison(BlockPiece source)
    {
        if (poisons.Count >= PoisonCap) return;

        Vector2 point = SkillOrigin(source);
        float radius = cell * Rule.poisonRadiusInCells;
        DamagePoison(point, radius);

        var view = CreatePoisonView(point, radius);
        var renderer = view != null ? view.GetComponentInChildren<SpriteRenderer>(true) : null;
        poisons.Add(new PoisonZone
        {
            point = point,
            radius = radius,
            view = view,
            sprite = renderer,
            color = renderer != null ? renderer.color : Color.white,
            scale = view != null ? view.localScale : Vector3.one
        });
    }

    /// <summary>同時に置ける沼。初期値は SO の poisonMax。スキルツリーの加算はあとから足す。</summary>
    int PoisonCap => Mathf.Max(0, Rule.poisonMax);

    void TickPoisons()
    {
        float exit = 0.25f;
        for (int i = poisons.Count - 1; i >= 0; i--)
        {
            var zone = poisons[i];
            zone.age += Time.deltaTime;
            float life = Mathf.Max(0.2f, Rule.poisonDuration);
            float fadeStart = Mathf.Max(0f, life - exit);
            if (zone.age < fadeStart)
            {
                zone.tick += Time.deltaTime;
                if (zone.tick >= Mathf.Max(0.05f, Rule.poisonInterval))
                {
                    zone.tick = 0f;
                    DamagePoison(zone.point, zone.radius);
                }
            }
            else if (zone.sprite != null)
            {
                float t = Mathf.Clamp01((zone.age - fadeStart) / Mathf.Max(0.05f, life - fadeStart));
                zone.view.localScale = zone.scale * (1f - t);
                var color = zone.color;
                color.a *= 1f - t;
                zone.sprite.color = color;
            }

            if (zone.age < life) continue;

            EndPoison(zone);
            poisons.RemoveAt(i);
        }
    }

    void DamagePoison(Vector2 point, float radius)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!Within(piece, point, radius)) continue;
            piece.Hit(Rule.ballDamage);
        }
    }

    Transform CreatePoisonView(Vector2 point, float radius)
    {
        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.poisonPool : null;
        GameObject instance;
        if (prefab != null)
            instance = Instantiate(prefab);
        else
        {
            instance = new GameObject("Poison");
            var sprite = instance.AddComponent<SpriteRenderer>();
            sprite.sprite = CircleSprite;
            sprite.color = new Color(0.4f, 0.95f, 0.35f, 0.45f);
            sprite.sortingOrder = 4;
        }

        var renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && renderer.sprite == null)
            renderer.sprite = CircleSprite;
        float spriteSize = renderer != null && renderer.sprite != null
            ? Mathf.Max(0.001f, renderer.sprite.bounds.size.x)
            : 1f;
        var authored = prefab != null ? prefab.transform.localScale : Vector3.one;
        float size = Mathf.Max(0.2f, radius * 2f) / spriteSize;
        instance.transform.position = new Vector3(point.x, point.y, 0f);
        instance.transform.localScale = new Vector3(
            Mathf.Sign(authored.x == 0f ? 1f : authored.x) * size * Mathf.Max(0.001f, Mathf.Abs(authored.x)),
            Mathf.Sign(authored.y == 0f ? 1f : authored.y) * size * Mathf.Max(0.001f, Mathf.Abs(authored.y)),
            authored.z == 0f ? 1f : authored.z);
        arena.Add(instance);
        return instance.transform;
    }

    void EndPoison(PoisonZone zone)
    {
        if (zone.view != null)
            Destroy(zone.view.gameObject);
    }

    void ShowBeam(Vector2 center, float length, bool horizontal)
    {
        var props = SOLoader.BlockSession;
        ShowBeam(center, length, horizontal ? 90f : 0f, props != null ? props.laserBeam : null);
    }

    void ShowBeam(Vector2 center, float length, float angle, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("レーザーのプレハブがありません。 SO_BlockSession を確認してください。");
            return;
        }

        var authored = prefab.transform.localScale;
        float thickness = Mathf.Abs(authored.x);
        if (thickness < 0.001f)
            thickness = Mathf.Max(0.08f, cell * 0.42f);
        float xSign = authored.x < 0f ? -1f : 1f;
        float ySign = authored.y < 0f ? -1f : 1f;

        var instance = Instantiate(prefab);
        instance.transform.SetPositionAndRotation(
            new Vector3(center.x, center.y, prefab.transform.localPosition.z),
            prefab.transform.rotation * Quaternion.Euler(0f, 0f, angle));
        var scale = new Vector3(xSign * thickness, ySign * length, authored.z == 0f ? 1f : authored.z);
        instance.transform.localScale = scale;

        var sprite = instance.GetComponent<SpriteRenderer>();
        if (sprite == null)
            sprite = instance.GetComponentInChildren<SpriteRenderer>(true);
        var color = sprite != null ? sprite.color : Color.white;
        flashes.Add(new BallFlash
        {
            transform = instance.transform,
            sprite = sprite,
            life = Rule.beamLife,
            hold = Rule.beamHold,
            shrinkShort = true,
            color = color,
            from = scale,
            to = scale
        });
        arena.Add(instance);
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

    /// <summary>カメラに少しでも重なっていれば真。</summary>
    public bool IsOnCamera(BlockPiece piece)
    {
        if (!AttackPlayfield.TryGetRect(out var view)) return false;

        var scale = piece.transform.lossyScale;
        float halfX = Mathf.Abs(scale.x) * 0.5f;
        float halfY = Mathf.Abs(scale.y) * 0.5f;
        var pos = piece.transform.position;
        return pos.x + halfX > view.xMin && pos.x - halfX < view.xMax
            && pos.y + halfY > view.yMin && pos.y - halfY < view.yMax;
    }

    static bool InColumn(BlockPiece piece, float x, float pad)
    {
        float half = Mathf.Abs(piece.transform.lossyScale.x) * 0.5f;
        return Mathf.Abs(piece.transform.position.x - x) <= half + pad;
    }

    static bool InRow(BlockPiece piece, float y, float pad)
    {
        float half = Mathf.Abs(piece.transform.lossyScale.y) * 0.5f;
        return Mathf.Abs(piece.transform.position.y - y) <= half + pad;
    }

    static bool OnDiagonal(BlockPiece piece, Vector2 origin, Vector2 direction, float pad)
    {
        Vector2 normal = new Vector2(-direction.y, direction.x);
        Vector2 rel = (Vector2)piece.transform.position - origin;
        float distance = Mathf.Abs(rel.x * normal.x + rel.y * normal.y);
        var scale = piece.transform.lossyScale;
        float reach = Mathf.Abs(scale.x) * 0.5f * Mathf.Abs(normal.x)
            + Mathf.Abs(scale.y) * 0.5f * Mathf.Abs(normal.y);
        return distance <= reach + pad;
    }

    void ShowFlash(Vector3 position, Sprite image, Vector3 from, Vector3 to, Color color, float life, float hold = 0f, bool shrinkShort = false)
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
            hold = Mathf.Max(0f, hold),
            shrinkShort = shrinkShort,
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
            if (flash.age < flash.hold)
            {
                flash.transform.localScale = flash.from;
                flash.sprite.color = flash.color;
                continue;
            }

            float exit = Mathf.Max(0.0001f, flash.life - flash.hold);
            float t = Mathf.Clamp01((flash.age - flash.hold) / exit);
            flash.transform.localScale = flash.shrinkShort
                ? ShrinkShort(flash.from, t)
                : Vector3.Lerp(flash.from, flash.to, t);
            var color = flash.color;
            color.a *= 1f - t;
            flash.sprite.color = color;
            if (t < 1f) continue;

            Destroy(flash.sprite.gameObject);
            flashes.RemoveAt(i);
        }
    }

    static Vector3 ShrinkShort(Vector3 from, float t)
    {
        var scale = from;
        if (Mathf.Abs(from.x) <= Mathf.Abs(from.y))
            scale.x = Mathf.Lerp(from.x, 0f, t);
        else
            scale.y = Mathf.Lerp(from.y, 0f, t);
        return scale;
    }

    sealed class PoisonZone
    {
        public Vector2 point;
        public float radius;
        public float age;
        public float tick;
        public Transform view;
        public SpriteRenderer sprite;
        public Color color;
        public Vector3 scale;
    }

    sealed class BallFlash
    {
        public Transform transform;
        public SpriteRenderer sprite;
        public float age;
        public float life;
        public float hold;
        public bool shrinkShort;
        public Color color;
        public Vector3 from;
        public Vector3 to;
    }

    void ArmBoss()
    {
        bossArmed = false;
        bossDue = false;
        bossSpawned = false;
        bossMissingLogged = false;
        bossTime = 0f;
        bossIndex = -1;

        var time = StageTimeManager.Inst;
        var table = SOLoader.StageData;
        if (time == null || table == null) return;

        var stage = table.FindStage(time.StageIndex);
        if (stage == null || stage.stageTime <= 0) return;
        if (!IsUncleared(stage.stageIndex)) return;

        bossArmed = true;
        bossTime = stage.stageTime;
        bossIndex = stage.bossIndex;
    }

    static bool IsUncleared(int stageIndex)
    {
        var save = SaveLoader.Inst;
        if (save == null) return true;
        return stageIndex > save.ClearedStageIndex;
    }

    void TickBoss()
    {
        if (!bossArmed || bossDue || bossSpawned || elapsed < bossTime) return;
        bossDue = true;
    }

    /// <summary>画面上端の外。山がすでに外まで届いていれば、その上。</summary>
    void SpawnDueBoss()
    {
        var prefab = FindBossPrefab(bossIndex);
        if (prefab == null)
        {
            if (!bossMissingLogged)
            {
                bossMissingLogged = true;
                Debug.LogWarning($"ボスが見つかりません。 bossIndex: {bossIndex}");
            }
            return;
        }

        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float stackedBottom = HighestLocalTop();
        float worldBottom = screenTop;
        if (!float.IsNegativeInfinity(stackedBottom))
        {
            float above = parentY + stackedBottom + Rule.blockGap * 0.5f;
            worldBottom = Mathf.Max(worldBottom, above);
        }

        float localBottom = worldBottom - parentY;
        float localLeft = -cell * columns * 0.5f;
        int cellsX = prefab.CellsX;
        int cellsY = prefab.CellsY;
        int x = (columns - cellsX) / 2;
        Place(prefab, x, 0, cellsX, cellsY, localLeft, localBottom);
        bossDue = false;
        bossSpawned = true;
    }

    void SpawnWave()
    {
        if (!StackOrigin(out float localLeft, out float localBottom)) return;

        if (bossDue)
            SpawnDueBoss();

        float fill = CurrentFill;
        var used = new bool[columns, Rows];
        ReserveBossCells(used, localLeft, localBottom);
        bool placed = false;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                if (used[x, y] || Random.value > fill) continue;
                if (TryPlace(used, x, y, localLeft, localBottom, PickPrefab()))
                    placed = true;
            }
        }

        if (!placed && !TryPlace(used, columns / 2, 0, localLeft, localBottom, PickPrefab()))
        {
            for (int y = 0; y < Rows && !placed; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (used[x, y]) continue;
                    if (!TryPlace(used, x, y, localLeft, localBottom, PickPrefab())) continue;
                    placed = true;
                    break;
                }
            }
        }
    }

    /// <summary>ボスのマスには通常ブロックを置かない。サイズはプレハブの横マス・縦マス。</summary>
    void ReserveBossCells(bool[,] used, float localLeft, float localBottom)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.BossIndex < 0) continue;

            var pos = piece.transform.localPosition;
            var scale = piece.transform.localScale;
            float minX = pos.x - scale.x * 0.5f;
            float maxX = pos.x + scale.x * 0.5f;
            float minY = pos.y - scale.y * 0.5f;
            float maxY = pos.y + scale.y * 0.5f;
            int x0 = Mathf.FloorToInt((minX - localLeft) / cell);
            int x1 = Mathf.FloorToInt((maxX - localLeft) / cell);
            int y0 = Mathf.FloorToInt((minY - localBottom) / cell);
            int y1 = Mathf.FloorToInt((maxY - localBottom) / cell);
            for (int y = y0; y <= y1; y++)
            {
                if (y < 0 || y >= Rows) continue;
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || x >= columns) continue;
                    used[x, y] = true;
                }
            }
        }
    }

    /// <summary>既存の山のすぐ上。通常群はここへ積んで、同じ速度で下ろす。</summary>
    bool StackOrigin(out float localLeft, out float localBottom)
    {
        localLeft = 0f;
        localBottom = 0f;
        if (formation == null) return false;

        float stackedBottom = HighestLocalTop();
        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float worldBottom = spawnFloorY;
        if (!float.IsNegativeInfinity(stackedBottom))
        {
            float above = parentY + stackedBottom + Rule.blockGap * 0.5f;
            worldBottom = Mathf.Max(worldBottom, above);
        }

        localBottom = worldBottom - parentY;
        localLeft = -cell * columns * 0.5f;
        return true;
    }

    void LoadBlockPrefabs()
    {
        if (blockPrefabs != null && blockPrefabs.Length > 0) return;
        blockPrefabs = Resources.LoadAll<BlockPiece>("Block");
    }

    BlockPiece FindBossPrefab(int index)
    {
        if (bossPrefabs == null || bossPrefabs.Length == 0)
            bossPrefabs = Resources.LoadAll<BlockPiece>("Boss");
        if (bossPrefabs == null) return null;

        for (int i = 0; i < bossPrefabs.Length; i++)
        {
            var prefab = bossPrefabs[i];
            if (prefab != null && prefab.BossIndex == index)
                return prefab;
        }
        return null;
    }

    BlockPiece PickPrefab()
    {
        LoadBlockPrefabs();
        if (blockPrefabs == null || blockPrefabs.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab != null && prefab.BossIndex < 0)
                total += prefab.Weight;
        }
        if (total <= 0f) return blockPrefabs[0];

        float roll = Random.value * total;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab == null || prefab.BossIndex >= 0) continue;
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

    bool CanOccupy(bool[,] used, int x, int y, int cellsX, int cellsY)
    {
        if (x < 0 || y < 0 || x + cellsX > columns || y + cellsY > Rows) return false;
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
        stepHp = Mathf.Max(1, Mathf.RoundToInt(Rule.baseHp * scale));
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

    float SizeScale(int cells, float hpScale)
    {
        float scale = 1f + (Mathf.Max(1, cells) - 1) * Rule.sizeHpScale;
        return scale * Mathf.Max(0f, hpScale);
    }

    void Place(BlockPiece prefab, int x, int y, int cellsX, int cellsY, float localLeft, float localBottom)
    {
        float spanWidth = cell * cellsX;
        float spanHeight = cell * cellsY;
        float width = Mathf.Max(0.02f, spanWidth - Rule.blockGap);
        float height = Mathf.Max(0.02f, spanHeight - Rule.blockGap);
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
