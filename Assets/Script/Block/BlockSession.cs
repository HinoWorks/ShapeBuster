using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ブロック崩しの1セッション。
/// 配置率、HP、下降速度、ボール数などの数値は SO_BlockSession。HPと速度はステージの倍率も掛ける。
/// 未クリアのステージは、stageTime の次の生成でボスを画面外に出す。
/// ブロックは1列ずつ。上に1.5マス空いたら出し、出した後は上端に0.5マス残す。デッドラインで終了する。
/// 狙った方向へ、手持ちを戻ってきた順に続けて打つ。下のラインで回収する。
/// 画面上端から launchTopInset までは、発射の基点にならない。
/// </summary>
public class BlockSession : MonoBehaviour
{
    public const int Rows = 6;
    public const int BlockLayer = 8;
    public const int BallLayer = 9;
    public const float BallDiameter = 0.32f;
    /// <summary>生成後に画面上端へ残す空き。ボールが潜り込む。</summary>
    const float TopClearanceCells = 0.5f;
    /// <summary>上端の空きがこれ以上になったら、1列出す。</summary>
    const float SpawnGapCells = 1.5f;
    const int OpeningRows = 3;
    const int ClearBonusCount = 10;

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

    SO_Player Player => SO_Player.Current;

    /// <summary>インゲームはブロック崩し。敵の湧きとカーソル射撃は使わない。</summary>
    public const bool Enabled = true;

    static Sprite squareSprite;
    static Sprite circleSprite;

    public static Sprite SquareSprite => squareSprite != null ? squareSprite : (squareSprite = MakeSprite(false));
    public static Sprite CircleSprite => circleSprite != null ? circleSprite : (circleSprite = MakeSprite(true));

    readonly List<BlockPiece> pieces = new();
    bool hadBreakable;
    readonly List<BlockWallBand> wallBands = new();
    float wallGimmickTimer;
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
    UI_BlockCursorInfo cursorInfo;
    RectTransform cursorBase;
    Image cursorBaseImage;
    readonly Vector3[] cursorCorners = new Vector3[4];
    BlockPiece[] blockPrefabs;
    const float BossStopDuration = 0.4f;

    bool bossArmed;
    bool bossDue;
    bool bossSpawned;
    bool bossMissingLogged;
    float bossTime;
    /// <summary>エンドレス（クリア済み）のとき、HPを上げはじめる秒数。ステージの stageTime。0 ならエンドレスではない。</summary>
    float endlessTime;
    int bossIndex;
    BlockPiece bossBreaking;
    float bossStopLeft;
    PhysicsMaterial2D bounce;
    BlockAimGuide aimGuide;
    bool running;
    bool pendingFail;
    /// <summary>デッドラインに着いてから、結果へ進むまでの残り秒数。場を止めて、着いたブロックを見せる。</summary>
    float failHoldLeft;
    readonly List<BlockPiece> failPieces = new();
    const float FailHoldDuration = 1.5f;
    const int FailPieceOrder = 20;
    bool aiming;
    bool volley;
    bool volleyFullStack;
    bool pinCursorUi;
    Vector2 pinnedScreen;
    float shotTimer;
    Vector2 shotDirection;
    float elapsed;
    float deadlineY;
    float ceilingY;
    float screenLeft;
    float screenRight;
    float screenBottom;
    float screenTop;
    float fieldLeft;
    float fieldRight;
    float cell;
    int columns;
    Vector2 launchAnchor;
    readonly List<BlockBall> turretShots = new();
    BlockTurret[] turrets;
    float[] turretTimers;
    BlockRowEmitter rowEmitter;
    float rowBeamTimer;
    readonly List<TurretFollowUp> turretFollowUps = new();
    const float TurretFollowGap = 0.18f;

    public bool IsRunning => running;

    /// <summary>デッドラインに着いて、結果へ進む前に場を止めている間。ステージの時間も進めない。</summary>
    public static bool FailHolds => Inst != null && Inst.failHoldLeft > 0f;

    /// <summary>
    /// 手元のボールを次に撃つ順に held へ、飛んでいるボールを away へ入れる（HUD の縦の列が使う）。
    /// 戻ったボールは手元の最後に並ぶ。
    /// </summary>
    public void GetHand(List<BlockBall> held, List<BlockBall> away)
    {
        held.Clear();
        away.Clear();
        if (!running || balls == null) return;
        for (int i = 0; i < handOrder.Count; i++)
        {
            var ball = handOrder[i];
            if (ball == null) continue;
            if (ball.IsHeld)
                held.Add(ball);
            else
                away.Add(ball);
        }
    }
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
        if (failHoldLeft > 0f)
        {
            failHoldLeft -= Time.unscaledDeltaTime;
            if (failHoldLeft <= 0f)
                FinishFail();
            return;
        }

        if (pendingFail)
        {
            pendingFail = false;
            Fail();
            return;
        }

        if (bossStopLeft > 0f)
        {
            bossStopLeft -= Time.unscaledDeltaTime;
            if (bossStopLeft > 0f)
            {
                if (running)
                    TryClearRefill();
                return;
            }
            FinishBossDefeat();
        }

        if (!running || CurtainHolds) return;

        TryClearRefill();
        TickBoss();
        TickWallGimmicks();
        if (TopGapReady())
            SpawnWave();
    }

    /// <summary>
    /// 画面切り替えの幕が画面を覆っている間は、場を止める。
    /// ブロックの下降・列の生成・照準と発射・タレット・壁の仕掛け・ボスの出現が、幕が開ききってから動き出す。
    /// </summary>
    public static bool CurtainHolds => TransitionCurtain.Inst != null && TransitionCurtain.Inst.IsCovering;

    void FixedUpdate()
    {
        if (!running || pendingFail || formationBody == null || CurtainHolds) return;

        elapsed += Time.fixedDeltaTime;
        formationBody.MovePosition(formationBody.position + Vector2.down * (BlockFallSpeed * Time.fixedDeltaTime));
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

    /// <summary>壊せるブロックが画面から無くなったらの処理。中身は一旦止めている。</summary>
    void TryClearRefill()
    {
        bool breakable = HasBreakableOnScreen();
        if (hadBreakable && !breakable)
        {
            // 全消し時の3列生成と、ボーナス（中央下にAAを10個）は一旦なし。
            // 空きを待たずに列を足すと、残った破壊不能ブロックと位置がずれて見え方が安定しなかった。
            // ボーナスの量と位置も合わせて見直すので、両方の処理は残して呼び出しだけ止めている。
            // SpawnClearRows(OpeningRows);
            // DropClearBonus();
            // breakable = HasBreakableOnScreen();
        }
        hadBreakable = breakable;
    }

    /// <summary>全消しボーナス。フィールド中央の下へ、AAをまとめて出す。呼び出しは一旦止めている。</summary>
    void DropClearBonus()
    {
        var manager = EnemyGenerateManager.Inst;
        if (manager == null) return;

        var point = new Vector3((fieldLeft + fieldRight) * 0.5f, deadlineY + cell * 3f, 0f);
        for (int i = 0; i < ClearBonusCount; i++)
        {
            var offset = (Vector3)(Random.insideUnitCircle * 0.4f);
            manager.SpawnLooseResource(point + offset, ResourceType.AA, BlockFallSpeed);
        }
    }

    float RowOrigin()
    {
        return screenTop - TopClearanceCells * cell - cell + Rule.blockGap * 0.5f;
    }

    float FormationY()
    {
        return formationBody != null ? formationBody.position.y : formation.position.y;
    }

    bool HasBreakableOnScreen()
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.IsSolid) continue;
            if (PlacedOnScreen(piece)) return true;
        }
        return false;
    }

    /// <summary>撃破の瞬間にゲームを止め、白く膨らんだ状態で保持する。止めたあと破壊する。</summary>
    public bool BeginBossDefeat(BlockPiece piece)
    {
        if (piece == null || bossBreaking != null) return false;

        bossBreaking = piece;
        bossStopLeft = BossStopDuration;
        piece.HoldBreak();
        SoundManager.SE(SEType.BossDefeat);
        StaticManager.ResetGameTime();
        Time.timeScale = 0f;
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Large);
        return true;
    }

    void FinishBossDefeat()
    {
        bossStopLeft = 0f;
        Time.timeScale = 1f;
        var piece = bossBreaking;
        bossBreaking = null;
        if (piece != null)
            piece.FinishBreak();
        StageTimeManager.Inst?.ClearByBoss();
    }

    /// <summary>
    /// 壊したブロックは資源を1種類だけ出す。
    /// 通常はAA、再生はBB、装甲はCC。ボスのFFはリザルトへ直行するため、その場には落とさず獲得へ足す。
    /// </summary>
    public void GrantBreakResource(BlockPiece piece, Vector3 point, int cells)
    {
        if (piece == null) return;

        if (piece.BossIndex >= 0)
        {
            int count = ScaleBreakCount(1, ResourceType.FF);
            if (count > 0)
                GameEvent.UI.PublishResourceMod_Ingame(ResourceType.FF, count);
            return;
        }

        if (piece.NoDrop)
            return;

        if (piece.OverridesDrop)
        {
            DropResources(point, 1, piece.DropResource);
            return;
        }

        if (piece.Regenerates)
        {
            DropResources(point, 1 + SkillTreeBonus.RegenDropAdd, ResourceType.BB);
            return;
        }

        if (piece.ArmorsBottom)
        {
            DropResources(point, 1 + SkillTreeBonus.ArmorDropAdd, ResourceType.CC);
            return;
        }

        float rewardRate = (1f + Mathf.Max(0f, SkillTreeBonus.BlockResourceUpRate))
            * (1f + SkillTreeBonus.BlockHarvestRate)
            * piece.RewardScale;
        int reward = StageEnemyScale.ScaleResourceCount(cells, rewardRate);
        // 「大漁」：確率でAが2倍になる
        if (reward > 0 && Random.value < SkillTreeBonus.BlockDoubleDropChance)
            reward *= 2;
        if (reward > 0)
            DropResources(point, reward, ResourceType.AA, piece.IsChest);
    }

    int ScaleBreakCount(int count, ResourceType type)
    {
        if (count <= 0) return 0;

        int stage = StageTimeManager.Inst != null ? StageTimeManager.Inst.activeStageIndex : -1;
        count = MilestoneBonus.ScaleDropCount(count, type, stage);
        // ステージの倍率は AA だけに掛ける。BB・CC・EE・ボスの FF は、どの段でも同じ数。
        if (type != ResourceType.AA) return count;
        var row = SOLoader.StageData != null ? SOLoader.StageData.FindStage(stage) : null;
        if (row != null && row.blockResourceRate > 0f)
            count = StageEnemyScale.ScaleResourceCount(count, row.blockResourceRate);
        return count;
    }

    /// <summary>
    /// 壊した位置へ資源を落とす。カーソルが触れるまで獲得にならない。
    /// 資源は壊した位置から、散らばる先へ弧を描いて弾け飛ぶ。
    /// - 普通：弧は低く、0.2〜0.35 秒で着地する。飛んでいる間も拾える。
    /// - chest（宝箱）：広く・高く・ゆっくり飛ばす。飛び終わるまでは拾えない。
    /// </summary>
    public void DropResources(Vector3 point, int count, ResourceType type = ResourceType.AA, bool chest = false)
    {
        var manager = EnemyGenerateManager.Inst;
        if (manager == null) return;

        count = ScaleBreakCount(count, type);
        if (count <= 0) return;

        // 数が多いほど広く散らす。ひまわりの種の並び（黄金角）で置き、固まらないようにする。
        float radius = Mathf.Min(0.2f + 0.18f * Mathf.Sqrt(count - 1), 1.6f);
        // 宝箱は、勢いが見えるように少し遠くまで飛ばす。
        if (chest)
            radius = Mathf.Max(0.7f, radius * 1.3f);
        float turn = Random.value * Mathf.PI * 2f;
        float margin = 0.15f;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset;
            if (count == 1)
                offset = Random.insideUnitCircle * radius;
            else
            {
                float r = radius * Mathf.Sqrt((i + 0.5f) / count);
                float angle = turn + i * 2.39996f;
                offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r + Random.insideUnitCircle * 0.05f;
            }
            var spot = point + (Vector3)offset;
            spot.x = Mathf.Clamp(spot.x, fieldLeft + margin, fieldRight - margin);
            spot.y = Mathf.Clamp(spot.y, deadlineY + margin * 2f, ceilingY - margin * 2f);

            // 遠くへ飛ぶものほど時間をかけ、弧も高くする。少しずつばらして、一斉に止まらないようにする。
            float reach = offset.magnitude / Mathf.Max(0.01f, radius);
            float duration;
            float arc = Mathf.Lerp(0.25f, 0.7f, reach) * Random.Range(0.8f, 1.2f);
            if (chest)
                duration = Mathf.Lerp(0.32f, 0.55f, reach) * Random.Range(0.9f, 1.1f);
            else
            {
                duration = Mathf.Lerp(0.2f, 0.35f, reach) * Random.Range(0.95f, 1.05f);
                arc *= 0.5f;
            }
            manager.SpawnBurstResource(point, spot, type, BlockFallSpeed, duration, arc, !chest);
        }
    }

    float CurrentSpeed => (Rule.baseSpeed + elapsed * Rule.speedPerSecond) * blockSpeedRate * SkillTreeBonus.FallSpeedScale;
    public float BlockFallSpeed => CurrentSpeed;

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

    int CurrentHp => Mathf.Max(1, Mathf.RoundToInt(Rule.baseHp * blockHpRate * HpTimeRate));

    /// <summary>
    /// 時間で上がるHPの倍率。hpStepTime 秒で hpStepRate を1回掛ける。
    /// エンドレスでは、stageTime 秒から endlessHpInterval 秒ごとに endlessHpRate を掛けていく。
    /// </summary>
    float HpTimeRate
    {
        get
        {
            float rate = elapsed >= Rule.hpStepTime ? Rule.hpStepRate : 1f;
            if (endlessTime > 0f && elapsed >= endlessTime && Rule.endlessHpInterval > 0f)
            {
                int steps = Mathf.FloorToInt((elapsed - endlessTime) / Rule.endlessHpInterval) + 1;
                rate *= Mathf.Pow(Rule.endlessHpRate, steps);
            }
            return rate;
        }
    }

    void Begin()
    {
        Clear();
        ResolveStageRates();
        running = true;
        elapsed = 0f;
        BuildArena();
        LoadBlockPrefabs();
        ArmBoss();
        SpawnOpeningRows();
        SpawnBalls();
        SpawnTurrets();
        SpawnRowBeam();
        GameEvent.InGame.PublishStarted();
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
        StopTurretShots();
        aiming = false;
        aimingBall = null;
        if (aimGuide != null)
            aimGuide.Hide();
        SoundManager.SE(SEType.StageFail);
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Medium);
        Debug.Log("<color=yellow> ========= Deadline ========= </color>");

        // すぐには結果へ進まず、場を止めたまま、デッドラインに着いたブロックを見せる。
        if (Time.timeScale != 1f)
            Time.timeScale = 1f;
        BeginFailMarks();
        failHoldLeft = FailHoldDuration;
    }

    void FinishFail()
    {
        failHoldLeft = 0f;
        GameWatcher.Inst?.SetGameState(GameStateType.InGame_End);
    }

    /// <summary>デッドラインに着いたブロックを手前に出して、赤く点滅させる。</summary>
    void BeginFailMarks()
    {
        failPieces.Clear();
        CollectDeadlinePieces(failPieces);
        for (int i = 0; i < failPieces.Count; i++)
            failPieces[i].MarkFail(FailPieceOrder);
    }

    void Clear()
    {
        running = false;
        pendingFail = false;
        failHoldLeft = 0f;
        failPieces.Clear();
        aiming = false;
        volley = false;
        pinCursorUi = false;
        shotTimer = 0f;
        shotQueue.Clear();
        handOrder.Clear();
        flashes.Clear();
        poisons.Clear();
        bursts.Clear();
        wallBands.Clear();
        wallGimmickTimer = 0f;
        elapsed = 0f;
        bossArmed = false;
        bossDue = false;
        bossSpawned = false;
        bossMissingLogged = false;
        bossTime = 0f;
        endlessTime = 0f;
        bossIndex = -1;
        if (bossStopLeft > 0f || bossBreaking != null)
            Time.timeScale = 1f;
        bossStopLeft = 0f;
        bossBreaking = null;
        hadBreakable = false;
        pieces.Clear();

        if (formation != null)
            Destroy(formation.gameObject);
        formation = null;
        formationBody = null;
        balls = null;
        aimingBall = null;
        turretShots.Clear();
        turrets = null;
        turretTimers = null;
        turretFollowUps.Clear();
        rowEmitter = null;
        rowBeamTimer = 0f;
        cursorInfo = null;
        cursorBase = null;
        cursorBaseImage = null;
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

    /// <summary>メインカメラの見える範囲。正射影だけ。</summary>
    static bool TryGetView(out Rect rect)
    {
        var camera = Camera.main;
        if (camera == null || !camera.orthographic)
        {
            rect = default;
            return false;
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        var center = camera.transform.position;
        rect = Rect.MinMaxRect(
            center.x - halfWidth,
            center.y - halfHeight,
            center.x + halfWidth,
            center.y + halfHeight);
        return true;
    }

    void BuildArena()
    {
        if (!TryGetView(out var view))
            view = new Rect(-5f, -5f, 10f, 10f);

        deadlineY = view.yMin + 0.45f;
        ceilingY = view.yMax;
        screenLeft = view.xMin;
        screenRight = view.xMax;
        screenBottom = view.yMin;
        screenTop = view.yMax;

        int baseColumns = ResolveBaseColumns();
        float midX = (view.xMin + view.xMax) * 0.5f;
        float preferred = view.width / Rule.defaultColumns * Rule.blockSizeScale;
        float fitted = view.width / baseColumns;
        float baseCell = Mathf.Max(0.12f, Mathf.Min(preferred, fitted));
        float scale = Mathf.Clamp(SkillTreeBonus.BlockCellScale, 0.4f, 1f);
        cell = Mathf.Max(0.08f, baseCell * scale);
        columns = Mathf.Max(1, baseColumns + SkillTreeBonus.AddedColumns);
        float blockLeft = midX - cell * columns * 0.5f;
        float blockRight = midX + cell * columns * 0.5f;
        float edge = Rule.blockGap * 0.5f;
        float side = Mathf.Max(0f, Rule.wallSideGap);
        fieldLeft = blockLeft + edge - side;
        fieldRight = blockRight - edge + side;
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

        float outerLeft = Mathf.Min(view.xMin, fieldLeft) - Rule.wallOverscan;
        float outerRight = Mathf.Max(view.xMax, fieldRight) + Rule.wallOverscan;
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
        PlaceLaunchLimit();
        CreateAimGuide();
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
    static readonly int EdgeXId = Shader.PropertyToID("_EdgeX");
    static readonly int CuredId = Shader.PropertyToID("_Cured");

    /// <summary>
    /// 左右の帯（FieldSide シェーダー）に、フィールドに接する側の x を渡す。内側のふちをほんのり光らせるのに使う。
    /// 帯の本体（一番上の SpriteRenderer）だけに渡し、子の線には触らない。
    /// </summary>
    void MarkWallEdge(GameObject wall, float xMin, float xMax)
    {
        var fill = wall.GetComponent<SpriteRenderer>();
        if (fill == null || fill.sharedMaterial == null || !fill.sharedMaterial.HasProperty(EdgeXId)) return;
        float middle = (fieldLeft + fieldRight) * 0.5f;
        float edge = Mathf.Abs(xMin - middle) < Mathf.Abs(xMax - middle) ? xMin : xMax;
        var block = new MaterialPropertyBlock();
        fill.GetPropertyBlock(block);
        block.SetFloat(EdgeXId, edge);
        // ワクチンを撒く前と後で、帯の粒を変える（前は灰が降る、後は光が昇る）。
        block.SetFloat(CuredId, VaccineRuntime.Cleared ? 1f : 0f);
        fill.SetPropertyBlock(block);
    }

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
        MarkWallEdge(instance, xMin, xMax);

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

    /// <summary>
    /// 壁の仕掛け。wallGimmickInterval 秒ごとに、種類ごとに、ステージの開始秒数を過ぎていれば確率で出す。
    /// </summary>
    void TickWallGimmicks()
    {
        var stage = CurrentStage();
        if (stage == null || cell <= 0.001f) return;
        wallGimmickTimer += Time.deltaTime;
        float interval = Mathf.Max(0.5f, Rule.wallGimmickInterval);
        if (wallGimmickTimer < interval) return;
        wallGimmickTimer -= interval;

        if (RollWallBand(WallBandKind.Boost, stage.wallBoostStart, stage.wallBoostChance, Rule.wallBoostMax))
            SpawnWallBand(WallBandKind.Boost, Rule.wallBoostCells, Rule.wallBoostLife, Rule.wallBoostRate, Rule.wallBoostTime);
        if (RollWallBand(WallBandKind.Slow, stage.wallSlowStart, stage.wallSlowChance, Rule.wallSlowMax))
            SpawnWallBand(WallBandKind.Slow, Rule.wallSlowCells, Rule.wallSlowLife, Rule.wallSlowRate, Rule.wallSlowTime);
    }

    bool RollWallBand(WallBandKind kind, int start, int chance, int max)
    {
        if (chance <= 0 || elapsed < start) return false;
        int count = 0;
        for (int i = 0; i < wallBands.Count; i++)
        {
            if (wallBands[i] != null && wallBands[i].Kind == kind)
                count++;
        }
        return count < max && Random.value * 100f < chance;
    }

    /// <summary>壁の帯を、左右どちらかの壁の空いた高さに出す。ほかの帯（種類を問わない）とは重ねない。</summary>
    bool SpawnWallBand(WallBandKind kind, float cells, float life, float rate, float time)
    {
        float height = cell * Mathf.Max(0.5f, cells);
        float low = deadlineY + cell + height * 0.5f;
        float high = ceilingY - cell * Mathf.Max(0f, Rule.wallBandTopGap) - height * 0.5f;
        if (high < low) return false;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            int inward = Random.value < 0.5f ? 1 : -1;
            float y = Random.Range(low, high);
            bool overlaps = false;
            for (int i = 0; i < wallBands.Count; i++)
            {
                var other = wallBands[i];
                if (other == null || other.Side != -inward) continue;
                if (Mathf.Abs(other.CenterY - y) < (other.Height + height) * 0.5f + cell * 0.5f)
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps) continue;

            var prefab = kind == WallBandKind.Slow ? Rule.wallSlowBand : Rule.wallBoostBand;
            if (prefab == null)
            {
                Debug.LogWarning($"壁の帯（{kind}）のプレハブがありません。 SO_BlockSession を確認してください。");
                return false;
            }
            var band = Instantiate(prefab);
            var host = band.gameObject;
            float face = inward > 0 ? fieldLeft : fieldRight;
            band.Init(this, kind, face, y, height, inward, BallDiameter * 0.6f, life, rate, time);
            wallBands.Add(band);
            arena.Add(host);
            return true;
        }
        return false;
    }

    public void NotifyWallBandGone(BlockWallBand band)
    {
        wallBands.Remove(band);
        if (band != null)
            arena.Remove(band.gameObject);
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
        var prefab = Player.aimGuide;
        if (prefab == null)
        {
            Debug.LogWarning("発射方向UIのプレハブがありません。 SO_Player を確認してください。");
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
        var spawned = new List<BlockBall>(8);
        int normals = SkillTreeBonus.NormalBallCount;
        for (int i = 0; i < normals; i++)
            AddBall(spawned, BlockBallKind.Normal, true);

        AddCopies(spawned, BlockBallKind.Bomb);
        AddCopies(spawned, BlockBallKind.Laser);
        AddCopies(spawned, BlockBallKind.LaserRow);
        AddCopies(spawned, BlockBallKind.Orbit);
        AddCopies(spawned, BlockBallKind.Cross);
        AddCopies(spawned, BlockBallKind.Poison);
        AddCopies(spawned, BlockBallKind.Heavy);
        balls = spawned.ToArray();
        handOrder.Clear();
        handOrder.AddRange(spawned);
    }

    float BallSpeed(BlockBallKind kind, BallData data)
    {
        float speed = (data != null && data.baseSpeed > 0f ? data.baseSpeed : 13f) * SkillTreeBonus.BallSpeedScale;
        if (kind == BlockBallKind.Heavy)
            speed *= SkillTreeBonus.HeavySpeedScale / 0.3f;
        return speed;
    }

    static BallData BallInfo(BlockBallKind kind)
    {
        var table = SOLoader.BallData;
        return table != null ? table.Find(kind) : null;
    }

    static float BallArea(BlockBallKind kind, float fallback)
    {
        var data = BallInfo(kind);
        float radius = data != null && data.areaRadius > 0f ? data.areaRadius : fallback;
        return radius * (1f + MilestoneBonus.Area(kind));
    }

    static float PoisonDuration()
    {
        var data = BallInfo(BlockBallKind.Poison);
        float duration = data != null && data.effectDuration > 0f ? data.effectDuration : 2.5f;
        return duration + MilestoneBonus.PoisonDuration;
    }

    static float PoisonInterval()
    {
        var data = BallInfo(BlockBallKind.Poison);
        return data != null && data.effectInterval > 0f ? data.effectInterval : 0.4f;
    }

    void AddCopies(List<BlockBall> spawned, BlockBallKind kind)
    {
        int copies = SkillTreeBonus.BallCopies(kind);
        for (int i = 0; i < copies; i++)
            AddBall(spawned, kind, false);
    }

    void AddBall(List<BlockBall> spawned, BlockBallKind kind, bool always)
    {
        if (!always && !Owns(kind)) return;

        var data = SOLoader.BallData != null ? SOLoader.BallData.Find(kind) : null;
        var prefab = data != null ? data.pf : null;
        if (prefab == null)
        {
            Debug.LogWarning($"ボールプレハブがありません。 {kind}");
            return;
        }

        var ball = Instantiate(prefab);
        float size = data.baseSize > 0f ? data.baseSize : 1f;
        if (!Mathf.Approximately(size, 1f))
            ball.transform.localScale *= size;
        ball.Init(bounce, BallSpeed(kind, data), kind);
        ball.Hold(launchAnchor);
        ball.SetVisible(false);
        spawned.Add(ball);
        arena.Add(ball.gameObject);
    }

    /// <summary>
    /// 左下と右下の砲台。一番低いブロックへ、ノーマルと同じ威力の弾を一定間隔で撃つ。
    /// 弾は手持ちに戻さず、デッドラインを越えたら消す。
    /// </summary>
    void SpawnTurrets()
    {
        turrets = null;
        turretTimers = null;
        if (!TurretsEnabled()) return;
        if (TurretBallPrefab() == null)
        {
            Debug.LogWarning("砲台のボールプレハブがありません。 Ball シートのタレットか SO_Player を確認してください。");
            return;
        }

        float interval = TurretInterval();
        float y = deadlineY + 0.36f;
        var left = CreateTurret(new Vector2(fieldLeft + 0.28f, y));
        var right = CreateTurret(new Vector2(fieldRight - 0.28f, y));
        turrets = new[] { left, right };
        turretTimers = new[] { interval, interval * 0.5f };
    }

    bool TurretsEnabled()
    {
        var balls = SOLoader.BallData;
        return (balls != null && balls.debugTurrets) || SkillTreeBonus.OwnsTurret;
    }

    float TurretInterval()
    {
        return ShotInterval(BlockBallKind.Turret, 3f);
    }

    /// <summary>Ball シートの effectDuration。タレットとオートビームでは攻撃間隔（秒）。</summary>
    static float ShotInterval(BlockBallKind kind, float fallback)
    {
        var data = BallInfo(kind);
        float seconds = data != null && data.effectDuration > 0f ? data.effectDuration : fallback;
        float haste = 0f;
        if (kind == BlockBallKind.Turret)
            haste = SkillTreeBonus.TurretFireRate;
        else if (kind == BlockBallKind.AutoBeam)
            haste = SkillTreeBonus.RowBeamFireRate;
        return Mathf.Max(0.05f, seconds / (1f + haste));
    }

    static BlockBall TurretBallPrefab()
    {
        var data = BallInfo(BlockBallKind.Turret);
        return data != null ? data.pf : null;
    }

    BlockTurret CreateTurret(Vector2 position)
    {
        var balls = SOLoader.BallData;
        BlockTurret turret;
        if (balls != null && balls.turret != null)
            turret = Instantiate(balls.turret);
        else
        {
            Debug.LogWarning("砲台プレハブがありません。 SO_BallData を確認してください。");
            turret = CreateFallbackTurret();
        }

        turret.transform.position = new Vector3(position.x, position.y, 0f);
        arena.Add(turret.gameObject);
        return turret;
    }

    static BlockTurret CreateFallbackTurret()
    {
        var root = new GameObject("Turret");
        var turret = root.AddComponent<BlockTurret>();
        AddTurretMark(root.transform, "Base", Vector3.down * 0.16f, new Vector3(0.62f, 0.16f, 1f), new Color(0.35f, 0.28f, 0.22f), false);
        AddTurretMark(root.transform, "Body", Vector3.zero, Vector3.one * 0.36f, new Color(0.55f, 0.38f, 0.18f), true);
        AddTurretMark(root.transform, "Barrel", Vector3.up * 0.28f, new Vector3(0.16f, 0.5f, 1f), new Color(1f, 0.72f, 0.28f), false);
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = Vector3.up * 0.7f;
        return turret;
    }

    static void AddTurretMark(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color, bool circle)
    {
        var mark = new GameObject(name);
        mark.transform.SetParent(parent, false);
        mark.transform.localPosition = localPosition;
        mark.transform.localScale = scale;
        var sprite = mark.AddComponent<SpriteRenderer>();
        sprite.sprite = circle ? CircleSprite : SquareSprite;
        sprite.color = color;
        sprite.sortingOrder = 4;
    }

    void TickTurrets()
    {
        if (turrets == null || turretTimers == null) return;

        float interval = TurretInterval();
        for (int i = 0; i < turrets.Length; i++)
        {
            var turret = turrets[i];
            if (turret == null) continue;

            var target = LowestPiece(turret.Muzzle);
            if (target == null) continue;

            turret.Aim(target.Center);
            turretTimers[i] += Time.deltaTime;
            if (turretTimers[i] < interval) continue;

            turretTimers[i] = 0f;
            FireTurret(turret, target);
            QueueTurretFollowUp(turret);
        }
    }

    void QueueTurretFollowUp(BlockTurret turret)
    {
        int extra = SkillTreeBonus.TurretExtraShots;
        if (extra <= 0 || turret == null) return;
        for (int i = 0; i < turretFollowUps.Count; i++)
        {
            if (turretFollowUps[i].turret == turret)
                return;
        }
        turretFollowUps.Add(new TurretFollowUp { turret = turret, left = extra });
    }

    void TickTurretFollowUps()
    {
        for (int i = turretFollowUps.Count - 1; i >= 0; i--)
        {
            var follow = turretFollowUps[i];
            if (follow.turret == null)
            {
                turretFollowUps.RemoveAt(i);
                continue;
            }

            follow.timer += Time.deltaTime;
            if (follow.timer < TurretFollowGap) continue;

            var target = LowestPiece(follow.turret.Muzzle);
            if (target == null) continue;

            follow.timer = 0f;
            follow.turret.Aim(target.Center);
            FireTurret(follow.turret, target);
            follow.left--;
            if (follow.left <= 0)
                turretFollowUps.RemoveAt(i);
        }
    }

    /// <summary>一番下のブロック。同じ高さなら、その砲台に近い方。</summary>
    BlockPiece LowestPiece(Vector2 from)
    {
        float lowest = float.PositiveInfinity;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            if (piece.Bottom < lowest)
                lowest = piece.Bottom;
        }
        if (float.IsPositiveInfinity(lowest)) return null;

        BlockPiece best = null;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            if (piece.Bottom > lowest + 0.02f) continue;

            float dist = ((Vector2)piece.Center - from).sqrMagnitude;
            if (dist >= bestDist) continue;
            best = piece;
            bestDist = dist;
        }
        return best;
    }

    void FireTurret(BlockTurret turret, BlockPiece target)
    {
        var prefab = TurretBallPrefab();
        if (prefab == null || turret == null || target == null) return;

        var data = BallInfo(BlockBallKind.Turret);
        var ball = Instantiate(prefab);
        float size = data != null && data.baseSize > 0f ? data.baseSize : 1f;
        if (!Mathf.Approximately(size, 1f))
            ball.transform.localScale *= size;

        ball.Init(bounce, BallSpeed(BlockBallKind.Turret, data), BlockBallKind.Turret);
        Vector2 origin = turret.Muzzle;
        Vector2 dir = (Vector2)target.Center - origin;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector2.up;
        dir.Normalize();

        float pad = BallDiameter;
        Vector2 spawn = origin + dir * pad;
        spawn.x = Mathf.Clamp(spawn.x, fieldLeft + pad, fieldRight - pad);
        spawn.y = Mathf.Max(spawn.y, deadlineY + pad);
        ball.Hold(spawn);
        ball.Fly(dir);
        turretShots.Add(ball);
        arena.Add(ball.gameObject);
    }

    void RecycleTurretShots()
    {
        for (int i = turretShots.Count - 1; i >= 0; i--)
        {
            var ball = turretShots[i];
            if (ball == null)
            {
                turretShots.RemoveAt(i);
                continue;
            }
            if (ball.Bottom > deadlineY) continue;

            PlayBallEffect(EffectType.BallDeadline, ball.transform.position);
            turretShots.RemoveAt(i);
            arena.Remove(ball.gameObject);
            Destroy(ball.gameObject);
        }
    }

    void StopTurretShots()
    {
        for (int i = 0; i < turretShots.Count; i++)
        {
            if (turretShots[i] != null)
                turretShots[i].Stop();
        }
    }

    /// <summary>
    /// 左の壁の外から、一番下のブロックの高さへ横ビームを撃つ。
    /// 発射体はその高さへ移動し、ビームは数秒に一度出す。
    /// </summary>
    void SpawnRowBeam()
    {
        rowEmitter = null;
        rowBeamTimer = 0f;
        if (!RowBeamEnabled()) return;
        var balls = SOLoader.BallData;
        if (balls == null || balls.rowBeam == null)
        {
            Debug.LogWarning("横ビームのプレハブがありません。 SO_BallData を確認してください。");
            return;
        }

        float x = Mathf.Clamp(fieldLeft - 0.55f, screenLeft + 0.25f, fieldLeft - 0.2f);
        var piece = LowestPiece(new Vector2(x, deadlineY));
        float y = piece != null ? piece.Center.y : (deadlineY + ceilingY) * 0.5f;
        rowEmitter = CreateRowEmitter(new Vector2(x, y));
    }

    bool RowBeamEnabled()
    {
        var balls = SOLoader.BallData;
        return (balls != null && balls.debugRowBeam) || SkillTreeBonus.OwnsRowBeam;
    }

    BlockRowEmitter CreateRowEmitter(Vector2 position)
    {
        var balls = SOLoader.BallData;
        BlockRowEmitter emitter;
        if (balls != null && balls.rowEmitter != null)
            emitter = Instantiate(balls.rowEmitter);
        else
        {
            Debug.LogWarning("横ビームの発射体がありません。 SO_BallData を確認してください。");
            emitter = CreateFallbackRowEmitter();
        }

        emitter.transform.position = new Vector3(position.x, position.y, 0f);
        arena.Add(emitter.gameObject);
        return emitter;
    }

    static BlockRowEmitter CreateFallbackRowEmitter()
    {
        var root = new GameObject("RowEmitter");
        var emitter = root.AddComponent<BlockRowEmitter>();
        AddTurretMark(root.transform, "Body", Vector3.zero, Vector3.one * 0.36f, new Color(0.28f, 0.42f, 0.48f), true);
        AddTurretMark(root.transform, "Barrel", Vector3.right * 0.28f, new Vector3(0.5f, 0.16f, 1f), new Color(0.25f, 0.95f, 1f), false);
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = Vector3.right * 0.55f;
        return emitter;
    }

    void TickRowBeam()
    {
        if (rowEmitter == null) return;

        var target = LowestPiece(rowEmitter.Muzzle);
        if (target == null) return;

        float y = target.Center.y;
        var pos = rowEmitter.transform.position;
        var balls = SOLoader.BallData;
        float speed = Mathf.Max(0.1f, balls != null ? balls.rowBeamMoveSpeed : 8f);
        pos.y = Mathf.MoveTowards(pos.y, y, speed * Time.deltaTime);
        rowEmitter.transform.position = pos;

        rowBeamTimer += Time.deltaTime;
        if (rowBeamTimer < ShotInterval(BlockBallKind.AutoBeam, 3f)) return;
        if (Mathf.Abs(pos.y - y) > 0.12f) return;

        rowBeamTimer = 0f;
        FireRowBeam(pos.y);
    }

    void FireRowBeam(float y)
    {
        int damage = Mathf.Max(1, SkillTreeBonus.RoundDamage(SkillTreeBonus.AttackPower(BlockBallKind.AutoBeam)));
        float pad = RowBeamPad();
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!InRow(piece, y, pad)) continue;
            piece.Hit(damage, (int)BlockBallKind.AutoBeam);
        }

        float x0 = rowEmitter != null ? rowEmitter.Muzzle.x : fieldLeft;
        float x1 = fieldRight;
        float length = Mathf.Max(0.2f, x1 - x0);
        var balls = SOLoader.BallData;
        ShowBeam(new Vector2((x0 + x1) * 0.5f, y), length, 90f, balls != null ? balls.rowBeam : null);
    }

    float RowBeamPad()
    {
        var balls = SOLoader.BallData;
        var prefab = balls != null ? balls.rowBeam : null;
        if (prefab == null) return 0.14f;
        return Mathf.Max(0.04f, Mathf.Abs(prefab.transform.localScale.x) * 0.5f);
    }

    static bool Owns(BlockBallKind kind)
    {
        return SkillTreeBonus.OwnsBall(kind);
    }

    void CreateCursorInfo()
    {
        var canvas = UIManager_Ingame.Inst != null ? UIManager_Ingame.Inst.DynamicHudCanvas : null;
        if (canvas == null) return;

        var prefab = Player.cursorInfo;
        if (prefab == null)
        {
            Debug.LogWarning("追従UIのプレハブがありません。 SO_Player を確認してください。");
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

        var prefab = Player.cursorBase;
        if (prefab == null)
        {
            Debug.LogWarning("カーソル目印のプレハブがありません。 SO_Player を確認してください。");
            return;
        }

        cursorBase = Instantiate(prefab, canvas.transform);
        cursorBaseImage = cursorBase.GetComponentInChildren<Image>(true);
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
        if (cursorBaseImage != null)
            cursorBaseImage.color = CannotFire() ? UI_BlockCursorInfo.BlockedColor : Color.white;
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
        int launchable = CountLaunchable();
        cursorInfo.Show(launchable, total, screen, CannotFire(), !pinCursorUi && launchable <= 0);
    }

    bool CannotFire()
    {
        if (pinCursorUi) return false;
        if (CountLaunchable() <= 0) return true;
        return volley || !PointerWorld.IsInsidePlayfield
            || BlockUnderPointer() != null || AboveLaunchLimit();
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
        if (!running || balls == null || CurtainHolds) return;

        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball == null || ball.IsHeld || ball == aimingBall) continue;
            if (ball.Bottom <= deadlineY)
                Park(ball, EffectType.BallDeadline);
        }

        TickCatch();
        TickVolley();
        TickAim();
        TickCursorBase();
        TickCursorInfo();
        TickFlashes();
        TickBursts();
        TickPoisons();
        RecycleTurretShots();
        TickTurrets();
        TickTurretFollowUps();
        TickRowBeam();
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
        var fire = Keyboard.current != null ? Keyboard.current.fKey : null;
        if (mouse == null || fire == null)
        {
            aimGuide?.Hide();
            return;
        }

        // 発射できない場所（ブロックの上、発射エリアより上）で押したら、カーソルの横で知らせる。
        if (fire.wasPressedThisFrame && PointerWorld.IsInsidePlayfield && aimingBall == null && !volley
            && (BlockUnderPointer() != null || AboveLaunchLimit()))
        {
            SoundManager.SE(SEType.UI_Error);
            if (cursorInfo != null)
                cursorInfo.FlashNotLaunch();
        }

        if (fire.wasPressedThisFrame && PointerWorld.IsInsidePlayfield && aimingBall == null && !volley
            && BlockUnderPointer() == null && !AboveLaunchLimit())
        {
            var ready = FindReadyBall();
            if (ready == null) return;

            launchAnchor = ClampLaunch((Vector2)PointerWorld.Position);
            pinnedScreen = mouse.position.ReadValue();
            pinCursorUi = true;
            aimingBall = ready;
            aiming = true;
        }

        if (!aiming || !fire.isPressed)
        {
            if (aiming && fire.wasReleasedThisFrame)
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

        BuildAimPath(pull);
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

        for (int leg = 0; leg < Player.aimBounces && left > 0.05f; leg++)
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
            pos += hit.normal * Player.aimSkin;
        }
    }

    float PierceDistance(Collider2D collider, Vector2 direction, float radius)
    {
        var extents = collider.bounds.extents;
        float span = Mathf.Abs(extents.x * direction.x) + Mathf.Abs(extents.y * direction.y);
        return span * 2f + radius + Player.aimSkin;
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
        if (pull.magnitude < Player.minPull)
        {
            ball.SetVisible(false);
            pinCursorUi = false;
            return;
        }

        BeginVolley(ball, pull);
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
        volleyFullStack = volley && balls != null && balls.Length > 0 && CountLaunchable() == balls.Length;
        if (!volley)
            pinCursorUi = false;
        FireQueued();
    }

    void TickVolley()
    {
        if (!volley || shotQueue.Count == 0) return;

        shotTimer += Time.deltaTime;
        float interval = MilestoneBonus.ShotInterval;
        if (shotTimer < interval) return;

        shotTimer -= interval;
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
            ball.SetFullStack(volleyFullStack);
            ball.Fly(shotDirection);
            SoundManager.SE(SEType.BallLaunch);
            MilestoneRuntime.NotifyLaunch();
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
        float pad = LaunchPad();
        point.x = Mathf.Clamp(point.x, fieldLeft + pad, fieldRight - pad);
        point.y = Mathf.Clamp(point.y, deadlineY + pad, LaunchOriginMaxY());
        return point;
    }

    /// <summary>画面上端から launchTopInset までを除いた、基点の上限。</summary>
    float LaunchOriginMaxY()
    {
        float pad = LaunchPad();
        float limit = ceilingY - Mathf.Max(0f, Player.launchTopInset);
        return Mathf.Clamp(limit, deadlineY + pad, ceilingY - pad);
    }

    bool ShowsLaunchLimit()
    {
        return Player.launchTopInset > LaunchPad() + 0.02f;
    }

    bool AboveLaunchLimit()
    {
        if (!ShowsLaunchLimit() || !PointerWorld.IsInsidePlayfield) return false;
        return PointerWorld.Position.y > LaunchOriginMaxY();
    }

    static float LaunchPad()
    {
        return BallDiameter * 0.5f + 0.04f;
    }

    /// <summary>
    /// 基点にできない上側の境界。点の見た目はプレハブ。ボールは通過できる。
    /// </summary>
    void PlaceLaunchLimit()
    {
        if (!ShowsLaunchLimit()) return;

        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.launchLimit : null;
        if (prefab == null)
        {
            Debug.LogWarning("発射エリアの線のプレハブがありません。 SO_BlockSession を確認してください。");
            return;
        }

        var instance = Instantiate(prefab);
        var line = instance.GetComponent<BlockLaunchLimit>();
        if (line == null)
            line = instance.AddComponent<BlockLaunchLimit>();
        line.Place(fieldLeft, fieldRight, LaunchOriginMaxY());
        arena.Add(instance);
    }

    /// <summary>
    /// 発射エリアへ戻ってきたボールにカーソルが触れたら回収する。
    /// 上のエリアを出てから、y速度が下向きのときだけ。撃った直後の下向きは対象にしない。
    /// </summary>
    void TickCatch()
    {
        if (!SkillTreeBonus.CatchReturning || balls == null) return;

        float limit = LaunchOriginMaxY();
        Vector2 pointer = PointerWorld.Position;
        bool canTouch = PointerWorld.IsInsidePlayfield && pointer.y <= limit;
        float reach = canTouch ? CursorReach() : 0f;

        for (int i = 0; i < balls.Length; i++)
        {
            var ball = balls[i];
            if (ball == null || ball.IsHeld) continue;

            Vector2 center = ball.transform.position;
            if (center.y > limit)
                ball.NoteAboveLaunch();
            if (!canTouch || !ball.WentAboveLaunch || ball.Velocity.y >= 0f) continue;
            if (center.y > limit) continue;

            float range = reach + BallRadius(ball);
            if (((Vector2)center - pointer).sqrMagnitude > range * range) continue;
            Park(ball, EffectType.BallCatch);
        }
    }

    /// <summary>カーソル画像の半径。プレイ平面のワールド単位。</summary>
    float CursorReach()
    {
        var camera = Camera.main;
        if (cursorBase == null || camera == null || !camera.orthographic || camera.pixelHeight <= 0)
            return 0f;

        var canvas = cursorBase.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        cursorBase.GetWorldCorners(cursorCorners);
        Vector2 left = RectTransformUtility.WorldToScreenPoint(uiCamera, cursorCorners[0]);
        Vector2 right = RectTransformUtility.WorldToScreenPoint(uiCamera, cursorCorners[3]);
        float pixels = Vector2.Distance(left, right) * 0.5f;
        return pixels * (camera.orthographicSize * 2f / camera.pixelHeight);
    }

    static void PlayBallEffect(EffectType effect, Vector3 worldPosition)
    {
        if (EffectManager.Inst == null) return;
        worldPosition.z = 0f;
        EffectManager.Inst.Rent(effect, worldPosition);
    }

    void Park(BlockBall ball, EffectType effect)
    {
        if (ball == null) return;
        Vector3 at = effect == EffectType.BallCatch
            ? PointerWorld.Position
            : ball.transform.position;
        PlayBallEffect(effect, at);
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
        MilestoneRuntime.NotifyReturn();
    }

    /// <summary>ボールスキルの発生位置。当たったブロックの中心を使う。</summary>
    public static Vector2 SkillOrigin(BlockPiece piece)
    {
        return piece != null ? piece.Center : Vector2.zero;
    }

    /// <summary>
    /// 分裂。倒されたブロックの位置に、小さいゾンビを横に並べて出す。
    /// 小さいゾンビの総 HP は親の総 HP × splitHpRate。色の段の刻みは通常と同じ。
    /// </summary>
    public void SpawnSplit(BlockPiece parent)
    {
        if (parent == null || !parent.Splits || formation == null) return;

        var prefab = parent.SplitInto;
        int count = parent.SplitCount;
        float scale = prefab.SizeScale;
        float span = cell * scale;
        float size = Mathf.Max(0.02f, span - Rule.blockGap * scale);
        Vector3 center = formation.InverseTransformPoint(parent.transform.position);
        float left = center.x - span * count * 0.5f;

        int stepHp = Mathf.Max(1, Mathf.RoundToInt(Rule.baseHp * prefab.HpScale));
        SplitTotal(parent.SplitHp, stepHp, out int level, out int hp, out int barMax);
        for (int i = 0; i < count; i++)
        {
            var piece = Instantiate(prefab, formation);
            piece.transform.localPosition = new Vector3(left + span * (i + 0.5f), center.y, 0f);
            piece.transform.localScale = new Vector3(size, size, 1f);
            // 左の子は右の縁に、右の子は左の縁に、切られた縫い目が残る。
            piece.SetSplitSide(count == 1 ? 0f : (i < count * 0.5f ? -1f : 1f));
            piece.Spawn(this, new Vector2(size, size), level, hp, barMax, stepHp, 1, 1);
            pieces.Add(piece);
        }
    }

    /// <summary>
    /// ガラスが割れた。ガラスのいたマスに、中のゾンビを出す。
    /// 種類は、その段で出るゾンビからマスごとに出現重みで選ぶ（ガラスとボスは除く。収まる大きさのものだけ）。
    /// 選べるものがないときは、プレハブの中身（contents）を出す。HP は、その時点で新しく出るブロックと同じ。
    /// </summary>
    public void ReleaseContents(BlockPiece glass)
    {
        if (glass == null || !glass.ReleasesContents || formation == null) return;

        // プレハブの大きさではなく、実際に置かれた大きさで並べる。
        // 端で入りきらず 1×1 に縮めて置いたガラスから 2×2 分を出すと、壁の外へはみ出す。
        int cellsX = glass.PlacedCellsX;
        int cellsY = glass.PlacedCellsY;
        Vector3 center = glass.SlotLocalPosition;
        float left = center.x - cell * cellsX * 0.5f;
        float bottom = center.y - cell * cellsY * 0.5f;
        var used = new bool[cellsX, cellsY];
        for (int y = 0; y < cellsY; y++)
        {
            for (int x = 0; x < cellsX; x++)
            {
                if (used[x, y]) continue;
                int cx = x, cy = y;
                var prefab = PickPrefab(p => !p.ReleasesContents && !p.IsChest && FitsInside(used, cx, cy, p.CellsX, p.CellsY));
                if (prefab == null)
                    prefab = glass.Contents;
                int w = Mathf.Max(1, prefab.CellsX);
                int h = Mathf.Max(1, prefab.CellsY);
                if (!FitsInside(used, x, y, w, h)) { w = 1; h = 1; }
                Occupy(used, x, y, w, h);
                Place(prefab, x, y, w, h, left, bottom);
            }
        }

        // 破片：四隅に火花を散らし、白い四角を広げる。
        SoundManager.SE(SEType.ArmorDeflect);
        Vector3 world = glass.transform.position;
        float spread = cell * 0.5f;
        if (EffectManager.Inst != null)
        {
            for (int i = 0; i < 4; i++)
            {
                var dir = new Vector3(i % 2 == 0 ? -1f : 1f, i < 2 ? -1f : 1f, 0f);
                EffectManager.Inst.Rent(EffectType.ShellSpark, world + dir * spread, Quaternion.FromToRotation(Vector3.up, dir));
            }
        }
        ShowFlash(world, SquareSprite, Vector3.one * cell * 1.6f, Vector3.one * cell * 2.4f, new Color(0.8f, 0.95f, 1f, 0.6f), 0.2f);
    }

    /// <summary>ガラスの中（used の範囲）に、はみ出さず空いたマスだけで収まる。</summary>
    static bool FitsInside(bool[,] used, int x, int y, int cellsX, int cellsY)
    {
        if (x + cellsX > used.GetLength(0) || y + cellsY > used.GetLength(1)) return false;
        for (int iy = 0; iy < cellsY; iy++)
        {
            for (int ix = 0; ix < cellsX; ix++)
            {
                if (used[x + ix, y + iy]) return false;
            }
        }
        return true;
    }

    /// <summary>総 HP を、色の段（1 段 stepHp）と、今の段の残り HP に分ける。段の数は色数で止める。</summary>
    static void SplitTotal(int total, int stepHp, out int level, out int current, out int barMax)
    {
        total = Mathf.Max(stepHp, total);
        level = Mathf.Max(1, Mathf.CeilToInt(total / (float)stepHp));
        current = total - (level - 1) * stepHp;
        if (current <= 0)
            current = stepHp;
        barMax = stepHp;

        int cap = SO_BlockLevel.MaxCount;
        if (level > cap)
        {
            current += (level - cap) * stepHp;
            level = cap;
            barMax = current;
        }
    }

    /// <summary>膨張の爆発。倒された位置から少し遅らせて広がり、巻き込んだ膨張がまた爆発して連鎖する。</summary>
    struct PendingBurst
    {
        public Vector2 point;
        public float radius;
        public int damage;
        public float delay;
    }

    const float BurstDelay = 0.12f;
    static readonly Color BurstColor = new Color(0.72f, 0.95f, 0.25f, 0.85f);
    readonly List<PendingBurst> bursts = new List<PendingBurst>();

    public void QueueBurst(BlockPiece source)
    {
        if (source == null || !source.Bursts) return;
        bursts.Add(new PendingBurst
        {
            point = source.Center,
            radius = cell * source.BurstRadius,
            damage = source.BurstDamage,
            delay = BurstDelay
        });
        // 膨らみきった合図として、先に小さく光らせる。
        ShowFlash(source.Center, CircleSprite, Vector3.one * 0.1f, Vector3.one * cell * 0.9f, BurstColor, BurstDelay);
    }

    void TickBursts()
    {
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            var burst = bursts[i];
            burst.delay -= Time.deltaTime;
            if (burst.delay > 0f)
            {
                bursts[i] = burst;
                continue;
            }
            bursts.RemoveAt(i);
            Burst(burst);
        }
    }

    void Burst(PendingBurst burst)
    {
        SoundManager.SE(SEType.Explosion);
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Small);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!Within(piece, burst.point, burst.radius)) continue;
            piece.Hit(burst.damage);
        }

        ShowFlash(
            burst.point,
            CircleSprite,
            Vector3.one * 0.2f,
            Vector3.one * burst.radius * 2f,
            BurstColor,
            0.34f);
    }

    public void Explode(BlockPiece source, int damage, bool critical = false, bool fullStack = false)
    {
        SoundManager.SE(SEType.Explosion);
        Vector2 point = SkillOrigin(source);
        float radius = cell * BallArea(BlockBallKind.Bomb, 2.5f);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!Within(piece, point, radius)) continue;
            piece.Hit(Mathf.Max(1, damage), (int)BlockBallKind.Bomb, critical, fullStack);
        }

        ShowFlash(
            point,
            CircleSprite,
            Vector3.one * 0.2f,
            Vector3.one * radius * 2f,
            new Color(1f, 0.45f, 0.12f, 0.8f),
            0.36f);
    }

    public void Beam(BlockPiece source, int damage, bool critical = false, bool fullStack = false)
    {
        SoundManager.SE(SEType.Laser);
        float x = SkillOrigin(source).x;
        float pad = cell * 0.55f;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!InColumn(piece, x, pad)) continue;
            piece.Hit(Mathf.Max(1, damage), (int)BlockBallKind.Laser, critical, fullStack);
        }

        ShowBeam(new Vector2(x, (screenBottom + screenTop) * 0.5f), Mathf.Max(0.2f, screenTop - screenBottom), false);
    }

    public void BeamRow(BlockPiece source, int damage, bool critical = false, bool fullStack = false)
    {
        SoundManager.SE(SEType.Laser);
        float y = SkillOrigin(source).y;
        float pad = cell * 0.55f;
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!InRow(piece, y, pad)) continue;
            piece.Hit(Mathf.Max(1, damage), (int)BlockBallKind.LaserRow, critical, fullStack);
        }

        ShowBeam(new Vector2((screenLeft + screenRight) * 0.5f, y), Mathf.Max(0.2f, screenRight - screenLeft), true);
    }

    public void BeamCross(BlockPiece source, int damage, bool critical = false, bool fullStack = false)
    {
        SoundManager.SE(SEType.Laser);
        Vector2 origin = SkillOrigin(source);
        float pad = cell * 0.55f;
        var upRight = new Vector2(0.70710678f, 0.70710678f);
        var downRight = new Vector2(0.70710678f, -0.70710678f);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!OnDiagonal(piece, origin, upRight, pad) && !OnDiagonal(piece, origin, downRight, pad)) continue;
            piece.Hit(Mathf.Max(1, damage), (int)BlockBallKind.Cross, critical, fullStack);
        }

        float spanX = screenRight - screenLeft;
        float spanY = screenTop - screenBottom;
        float length = Mathf.Sqrt(spanX * spanX + spanY * spanY) * 2.5f;
        var props = SOLoader.BlockSession;
        var prefab = props != null ? props.laserBeamCross : null;
        ShowBeam(origin, length, 45f, prefab);
        ShowBeam(origin, length, -45f, prefab);
    }

    public void Poison(BlockPiece source, int damage, bool fullStack = false)
    {
        if (poisons.Count >= PoisonCap) return;
        SoundManager.SE(SEType.Poison);

        Vector2 point = SkillOrigin(source);
        float radius = cell * BallArea(BlockBallKind.Poison, 1.6f);
        int hit = Mathf.Max(1, damage);
        DamagePoison(point, radius, hit, fullStack);

        var view = CreatePoisonView(point, radius);
        var renderer = view != null ? view.GetComponentInChildren<SpriteRenderer>(true) : null;
        poisons.Add(new PoisonZone
        {
            point = point,
            radius = radius,
            damage = hit,
            fullStack = fullStack,
            view = view,
            sprite = renderer,
            color = renderer != null ? renderer.color : Color.white,
            scale = view != null ? view.localScale : Vector3.one
        });
    }

    /// <summary>同時に置ける沼。初期値はボールデータの effectMax。</summary>
    int PoisonCap
    {
        get
        {
            var data = BallInfo(BlockBallKind.Poison);
            int max = data != null && data.effectMax > 0 ? data.effectMax : 2;
            return Mathf.Max(0, max);
        }
    }

    void TickPoisons()
    {
        float exit = 0.25f;
        for (int i = poisons.Count - 1; i >= 0; i--)
        {
            var zone = poisons[i];
            zone.age += Time.deltaTime;
            float life = Mathf.Max(0.2f, PoisonDuration());
            float fadeStart = Mathf.Max(0f, life - exit);
            if (zone.age < fadeStart)
            {
                zone.tick += Time.deltaTime;
                if (zone.tick >= Mathf.Max(0.05f, PoisonInterval()))
                {
                    zone.tick = 0f;
                    DamagePoison(zone.point, zone.radius, zone.damage, zone.fullStack);
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

    void DamagePoison(Vector2 point, float radius, int damage, bool fullStack)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || !IsOnCamera(piece)) continue;
            if (!Within(piece, point, radius)) continue;
            piece.Hit(Mathf.Max(1, damage), (int)BlockBallKind.Poison, false, fullStack);
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
            life = Player.beamLife,
            hold = Player.beamHold,
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
        if (!TryGetView(out var view)) return false;

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

    sealed class TurretFollowUp
    {
        public BlockTurret turret;
        public int left;
        public float timer;
    }

    sealed class PoisonZone
    {
        public Vector2 point;
        public float radius;
        public int damage = 1;
        public bool fullStack;
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
        endlessTime = 0f;
        bossIndex = -1;

        var time = StageTimeManager.Inst;
        var table = SOLoader.StageData;
        if (time == null || table == null) return;

        var stage = table.FindStage(time.StageIndex);
        if (stage == null || stage.stageTime <= 0) return;
        if (!IsUncleared(stage.stageIndex))
        {
            endlessTime = stage.stageTime;
            return;
        }

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
        float span = cell * Mathf.Max(1, prefab.CellsY);
        float worldBottom = screenTop - TopClearanceCells * cell - span + Rule.blockGap * 0.5f;
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
        SoundManager.SE(SEType.BossSpawn);
        SoundManager.BGM(BGMType.Boss);
    }

    void SpawnOpeningRows()
    {
        if (bossDue)
            SpawnDueBoss();
        if (formation == null) return;

        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float topBottom = screenTop - TopClearanceCells * cell - cell + Rule.blockGap * 0.5f;
        float localLeft = -cell * columns * 0.5f;
        for (int i = 0; i < OpeningRows; i++)
        {
            float worldBottom = topBottom - (OpeningRows - 1 - i) * cell;
            float localBottom = worldBottom - parentY;
            SpawnRow(localLeft, localBottom);
        }
    }

    /// <summary>画面上端から下へ、空いている列を出す。置けた数を返す。</summary>
    int SpawnClearRows(int count)
    {
        if (formation == null || cell <= 0.001f || count <= 0) return 0;

        float parentY = FormationY();
        float topBottom = RowOrigin();
        float localLeft = -cell * columns * 0.5f;
        int spawned = 0;
        for (int i = 0; spawned < count && i < 24; i++)
        {
            float worldBottom = topBottom - i * cell;
            if (worldBottom + cell < screenBottom) break;
            float localBottom = worldBottom - parentY;
            if (!RowHasGap(localLeft, localBottom)) continue;
            if (SpawnRow(localLeft, localBottom))
                spawned++;
        }

        if (bossDue)
            SpawnDueBoss();
        return spawned;
    }

    bool RowHasGap(float localLeft, float localBottom)
    {
        for (int x = 0; x < columns; x++)
        {
            if (!CellOccupied(localLeft, localBottom, x))
                return true;
        }
        return false;
    }

    bool CellOccupied(float localLeft, float localBottom, int x)
    {
        float minX = localLeft + x * cell;
        float maxX = minX + cell;
        float minY = localBottom;
        float maxY = localBottom + cell;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            if (OverlapsCell(piece, minX, maxX, minY, maxY))
                return true;
        }
        return false;
    }

    static bool OverlapsCell(BlockPiece piece, float minX, float maxX, float minY, float maxY)
    {
        var pos = piece.SlotLocalPosition;
        var scale = piece.PlacedScale;
        float left = pos.x - Mathf.Abs(scale.x) * 0.5f;
        float right = pos.x + Mathf.Abs(scale.x) * 0.5f;
        float bottom = pos.y - Mathf.Abs(scale.y) * 0.5f;
        float top = pos.y + Mathf.Abs(scale.y) * 0.5f;
        return left < maxX - 0.001f && right > minX + 0.001f
            && bottom < maxY - 0.001f && top > minY + 0.001f;
    }

    bool PlacedOnScreen(BlockPiece piece)
    {
        if (!TryGetView(out var view)) return false;

        var pos = piece.transform.position;
        var scale = piece.PlacedScale;
        float halfX = Mathf.Abs(scale.x) * 0.5f;
        float halfY = Mathf.Abs(scale.y) * 0.5f;
        return pos.x + halfX > view.xMin && pos.x - halfX < view.xMax
            && pos.y + halfY > view.yMin && pos.y - halfY < view.yMax;
    }

    void SpawnWave()
    {
        if (!TryNextRow(out float localLeft, out float localBottom)) return;

        if (bossDue)
            SpawnDueBoss();

        SpawnRow(localLeft, localBottom);
    }

    /// <summary>次の列は、いちばん高いブロックの底から1マス上。縦長が塞ぐ列はそのまま残す。</summary>
    bool TryNextRow(out float localLeft, out float localBottom)
    {
        localLeft = -cell * columns * 0.5f;
        localBottom = 0f;
        if (formation == null || cell <= 0.001f) return false;

        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float bottom = HighestVisualBottom();
        if (float.IsNegativeInfinity(bottom))
        {
            float worldBottom = screenTop - TopClearanceCells * cell - cell + Rule.blockGap * 0.5f;
            localBottom = worldBottom - parentY;
            return true;
        }

        float gap = Rule.blockGap;
        localBottom = bottom - gap * 0.5f + cell;
        float limit = HighestLocalTop() + gap;
        for (int step = 0; step < 8; step++)
        {
            if (RowHasGap(localLeft, localBottom))
                return true;
            if (localBottom > limit)
                return false;
            localBottom += cell;
        }
        return RowHasGap(localLeft, localBottom);
    }

    bool SpawnRow(float localLeft, float localBottom)
    {
        float fill = CurrentFill;
        var used = new bool[columns, 1];
        ReserveAliveCells(used, localLeft, localBottom);
        bool placed = false;

        for (int x = 0; x < columns; x++)
        {
            if (used[x, 0] || Random.value > fill) continue;
            if (TryPlace(used, x, 0, localLeft, localBottom, PickPrefab()))
                placed = true;
        }

        if (placed) return true;

        for (int x = 0; x < columns; x++)
        {
            int index = (columns / 2 + x) % columns;
            if (used[index, 0]) continue;
            if (TryPlace(used, index, 0, localLeft, localBottom, PickPrefab()))
                return true;
        }
        return false;
    }

    /// <summary>次の列の下端から、画面上端まで1.5マス以上空いている。</summary>
    bool TopGapReady()
    {
        if (formation == null || cell <= 0.001f) return false;
        if (float.IsNegativeInfinity(HighestVisualBottom())) return true;
        if (!TryNextRow(out _, out float localBottom)) return false;

        float parentY = formationBody != null ? formationBody.position.y : formation.position.y;
        float anchorTop = parentY + localBottom - Rule.blockGap;
        return screenTop - anchorTop >= SpawnGapCells * cell - 0.001f;
    }

    /// <summary>すでにいるブロックのマスには重ねない。</summary>
    void ReserveAliveCells(bool[,] used, float localLeft, float localBottom)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            MarkCells(used, piece, localLeft, localBottom);
        }
    }

    void MarkCells(bool[,] used, BlockPiece piece, float localLeft, float localBottom)
    {
        var pos = piece.SlotLocalPosition;
        var scale = piece.PlacedScale;
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
            if (y < 0 || y >= used.GetLength(1)) continue;
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= columns) continue;
                used[x, y] = true;
            }
        }
    }

    float HighestVisualBottom()
    {
        float bottom = float.NegativeInfinity;
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive) continue;
            float y = piece.SlotLocalPosition.y - Mathf.Abs(piece.PlacedScale.y) * 0.5f;
            if (y > bottom)
                bottom = y;
        }
        return bottom;
    }

    void LoadBlockPrefabs()
    {
        if (blockPrefabs != null && blockPrefabs.Length > 0) return;
        blockPrefabs = new[]
        {
            Rule.square,
            Rule.wide,
            Rule.tall,
            Rule.solid,
            Rule.armor,
            Rule.regen,
            Rule.bloat,
            Rule.split,
            Rule.ghost,
            Rule.glass,
            Rule.chest
        };
    }

    BlockPiece FindBossPrefab(int index)
    {
        var prefab = Rule.boss;
        if (prefab != null && prefab.BossIndex == index)
            return prefab;
        var list = Rule.bosses;
        if (list == null) return null;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].BossIndex == index)
                return list[i];
        }
        return null;
    }

    /// <summary>その段の出現重みでブロックを選ぶ。allow を渡すと、それを満たすものだけから選ぶ（ないときは null）。</summary>
    BlockPiece PickPrefab(System.Func<BlockPiece, bool> allow = null)
    {
        LoadBlockPrefabs();
        if (blockPrefabs == null || blockPrefabs.Length == 0) return null;

        var stage = CurrentStage();
        bool staged = stage != null && stage.HasBlockWeights;

        float normal = 0f;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab == null || prefab.BossIndex >= 0 || prefab.Regenerates || prefab.ArmorsBottom) continue;
            normal += staged ? stage.WeightOf(prefab) : prefab.Weight;
        }

        float total = 0f;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab != null && prefab.BossIndex < 0 && (allow == null || allow(prefab)))
                total += SpawnWeight(prefab, normal, stage, staged);
        }
        if (total <= 0f) return allow == null ? blockPrefabs[0] : null;

        float roll = Random.value * total;
        for (int i = 0; i < blockPrefabs.Length; i++)
        {
            var prefab = blockPrefabs[i];
            if (prefab == null || prefab.BossIndex >= 0 || (allow != null && !allow(prefab))) continue;
            roll -= SpawnWeight(prefab, normal, stage, staged);
            if (roll <= 0f) return prefab;
        }
        if (allow != null)
        {
            for (int i = blockPrefabs.Length - 1; i >= 0; i--)
            {
                var prefab = blockPrefabs[i];
                if (prefab != null && prefab.BossIndex < 0 && allow(prefab) && SpawnWeight(prefab, normal, stage, staged) > 0f)
                    return prefab;
            }
            return null;
        }
        return blockPrefabs[blockPrefabs.Length - 1];
    }

    StageData CurrentStage()
    {
        var time = StageTimeManager.Inst;
        var table = SOLoader.StageData;
        if (time == null || table == null) return null;
        return table.FindStage(time.StageIndex);
    }

    static float SpawnWeight(BlockPiece prefab, float normal, StageData stage, bool staged)
    {
        if (prefab.Regenerates)
        {
            float weight = staged ? stage.weightRegen : 0f;
            return weight + normal * SkillTreeBonus.RegenSpawnRate;
        }
        if (prefab.ArmorsBottom)
        {
            float weight = staged ? stage.weightArmor : 0f;
            return weight + normal * SkillTreeBonus.ArmorSpawnRate;
        }
        if (staged)
            return stage.WeightOf(prefab);
        return prefab.Weight;
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
        int rows = used.GetLength(1);
        if (x < 0 || y < 0 || x + cellsX > columns) return false;
        for (int iy = 0; iy < cellsY; iy++)
        {
            int row = y + iy;
            if (row >= rows) continue;
            for (int ix = 0; ix < cellsX; ix++)
            {
                if (used[x + ix, row]) return false;
            }
        }
        return true;
    }

    static void Occupy(bool[,] used, int x, int y, int cellsX, int cellsY)
    {
        int rows = used.GetLength(1);
        for (int iy = 0; iy < cellsY; iy++)
        {
            int row = y + iy;
            if (row >= rows) continue;
            for (int ix = 0; ix < cellsX; ix++)
                used[x + ix, row] = true;
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
            if (piece == null || !piece.IsAlive) continue;
            float localTop = piece.PlacedLocalTop;
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

    /// <summary>デッドラインに着いたブロックをすべて入れる（同時に着いたものも印をつける）。</summary>
    void CollectDeadlinePieces(List<BlockPiece> result)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece == null || !piece.IsAlive || piece.IsSolid) continue;
            if (piece.Bottom <= deadlineY)
                result.Add(piece);
        }
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
