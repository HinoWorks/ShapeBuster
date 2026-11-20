using DG.Tweening;
using UnityEngine;

/// <summary>
/// ブロック1個。プレハブのマス数、色、HP倍率、壊れない設定を使う。
/// 見た目は BlockLevelFill。ボスのプレハブは BlockBossFill（大きなゾンビの顔）を使う。
/// 現在レベルの色を下から塗り、空きは一つ下の色。レベル1の空きは透過。
/// 輪郭の太さはスケールが変わっても同じにする。
/// 被弾で白く光り、一瞬大きくなる。破壊時も同じ演出を出し、その場に破壊エフェクトを出す。
/// 装甲は下端か左右の端だけを受けない。出現のたびにどちらかになる。再生はヒットが途切れると失った段を戻す。
/// </summary>
public class BlockPiece : MonoBehaviour
{
    const string LevelShader = "ShapeBuster/BlockLevelFill";
    const string BossShader = "ShapeBuster/BlockBossFill";
    const string StoneShader = "ShapeBuster/BlockStoneFill";
    const float HitHold = 0.05f;
    const float HitReturn = 0.08f;
    const float HitScale = 1.18f;
    const float FlashFade = 0.09f;
    /// <summary>殻のドットを本体のドットの何分の一にするか。2 なら半分の大きさ。</summary>
    const int ShellDetail = 2;

    static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    static readonly int UnderColorId = Shader.PropertyToID("_UnderColor");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    static readonly int OutlineWidthXYId = Shader.PropertyToID("_OutlineWidthXY");
    static readonly int CellSpanId = Shader.PropertyToID("_CellSpan");
    static readonly int FlashId = Shader.PropertyToID("_Flash");
    static readonly int ZombieId = Shader.PropertyToID("_Zombie");
    static readonly int SeedId = Shader.PropertyToID("_Seed");
    static readonly int RegenGlowId = Shader.PropertyToID("_RegenGlow");
    static readonly int RegenChargeId = Shader.PropertyToID("_RegenCharge");
    static readonly int HealPulseId = Shader.PropertyToID("_HealPulse");
    static readonly int ArmorSideId = Shader.PropertyToID("_ArmorSide");
    static readonly int SplitSideId = Shader.PropertyToID("_SplitSide");
    static readonly int GhostFadeId = Shader.PropertyToID("_GhostFade");
    static readonly int ShellOpenId = Shader.PropertyToID("_ShellOpen");
    static readonly int ShellWarnId = Shader.PropertyToID("_ShellWarn");
    static readonly int ShellHitId = Shader.PropertyToID("_ShellHit");
    static readonly int PodCrackId = Shader.PropertyToID("_PodCrack");
    static readonly int GlassCrackId = Shader.PropertyToID("_GlassCrack");
    static readonly int ChestDamageId = Shader.PropertyToID("_ChestDamage");
    /// <summary>甲羅ボスの殻が回る前に、点滅して知らせる秒数。</summary>
    const float ShellWarnTime = 0.7f;
    /// <summary>すり抜けの前後に、見た目だけ先に薄くし始める秒数。</summary>
    const float GhostLead = 0.2f;
    static readonly int DotScaleId = Shader.PropertyToID("_DotScale");
    static readonly int ShellDotsId = Shader.PropertyToID("_Dots");
    static readonly int ShellThId = Shader.PropertyToID("_ShellTh");
    static readonly int ShellModeId = Shader.PropertyToID("_ShellMode");
    static readonly int ShellFlashId = Shader.PropertyToID("_Flash");

    static Material sharedFill;

    [Tooltip("横のマス数。ボスはこの幅に通常ブロックを出さない")]
    [SerializeField] int cellsX = 1;
    [Tooltip("縦のマス数。ボスはこの高さに通常ブロックを出さない")]
    [SerializeField] int cellsY = 1;
    [Tooltip("壊せない。デッドラインに触れても終了しない")]
    [SerializeField] bool solid;
    [Tooltip("出現しやすさ")]
    [SerializeField] float weight = 1f;
    [Tooltip("マス数による倍率に、さらに掛けるHP倍率")]
    [SerializeField] float hpScale = 1f;
    [Tooltip("ゾンビ図鑑の id（SO_BestiaryData）。0 なら、ブロックの性質から決める")]
    [SerializeField] int bestiaryId;
    [Tooltip("体の色。HP の量では変わらない。残り HP の割合だけ下から塗られ、減った分は透ける")]
    [SerializeField] Color bodyColor = new Color(0.64f, 0.72f, 0.60f, 1f);
    [Tooltip("0以上ならボス。ステージSOの bossIndex と一致させる。通常ブロックは -1")]
    [SerializeField] int bossIndex = -1;
    [Tooltip("下の面から来た弾はダメージにならない。爆発、レーザー、毒は通る。")]
    [SerializeField] bool armorBottom;
    [Tooltip("ヒットが途切れてから、失った段を1つ戻すまでの秒数。0 なら再生しない。")]
    [SerializeField] float regenDelay;
    [Tooltip("0 より大きいと膨張。倒すと、この半径（マス）の中のブロックに爆発のダメージを与える。")]
    [SerializeField] float burstRadius;
    [Tooltip("爆発のダメージ。出現したときの自分の総 HP に掛ける倍率。")]
    [SerializeField] float burstDamageRate = 1f;
    [Tooltip("通常の資源（AA）の数に掛ける倍率。")]
    [SerializeField] float rewardScale = 1f;
    [Tooltip("分裂。倒すと、このプレハブを splitCount 体その場に出す。空なら分裂しない。")]
    [SerializeField] BlockPiece splitInto;
    [Tooltip("分裂で出す数。横に並べる。")]
    [SerializeField] int splitCount = 2;
    [Tooltip("分裂した 1 体の総 HP。出現したときの自分の総 HP に掛ける倍率。")]
    [SerializeField] float splitHpRate = 0.4f;
    [Tooltip("1 マスに対する大きさ。分裂で出る小さいゾンビは 0.5。")]
    [SerializeField] float sizeScale = 1f;
    [Tooltip("オンなら、AA の代わりに dropResource を 1 個落とす。")]
    [SerializeField] bool overrideDrop;
    [SerializeField] ResourceType dropResource = ResourceType.EE;
    [Tooltip("0 より大きいと幽霊。実体でいる秒数。")]
    [SerializeField] float ghostSolidTime;
    [Tooltip("幽霊がすり抜けている秒数。この間はボールが当たらず、レーザーだけが当たる。")]
    [SerializeField] float ghostPhaseTime = 1.6f;
    [Tooltip("ガラス。割れると、その段で出るゾンビ（中に収まるもの）を出現重みで選んで、その場に出す。選べないときはこのプレハブを 1 マスに 1 体ずつ出す。空ならガラスではない")]
    [SerializeField] BlockPiece contents;
    [Tooltip("オンなら資源を落とさず、マイルストーンの破壊数にも数えない")]
    [SerializeField] bool noDrop;
    [Tooltip("宝箱。顔を描かず、揺れずに下りてくる。資源は通常の AA を、その段の 1 マスのゾンビの rewardScale 倍だけ落とす（大きさに関係なく 1 マスとして数える）。ステージの weightChest で出す")]
    [SerializeField] bool chest;
    [Tooltip("0 より大きいと甲羅ボス。殻の開いた面（下→右→上→左）が、この秒数ごとに回る。殻の面に当たったボールは弾かれる。爆発・レーザー・毒は殻を通る")]
    [SerializeField] float shellTurnTime;
    [Tooltip("0 より大きいと再生ボス。額の種が溜まるまでの秒数。溜まると HP を回復する。HP が減っていないときは溜めない")]
    [SerializeField] float bossHealTime;
    [Tooltip("再生ボスの 1 回の回復量。出現したときの総 HP に掛ける倍率")]
    [SerializeField] float bossHealRate = 0.2f;
    [Tooltip("種が溜まる間に、この量（出現したときの総 HP に掛ける倍率）のダメージを与えると、種が割れて回復しない")]
    [SerializeField] float bossHealBreakRate = 0.06f;
    [Tooltip("種が割れたあと、また溜め始めるまでの秒数")]
    [SerializeField] float bossHealStun = 2.5f;
    [SerializeField] SpriteRenderer sprite;
    [Tooltip("装甲の板と山形。下から弾かれたとき白く光る。")]
    [SerializeField] SpriteRenderer[] plateMarks;
    [Tooltip("再生の中央印。溜まると大きくなる。")]
    [SerializeField] Transform regenMark;

    BlockSession session;
    MaterialPropertyBlock propertyBlock;
    int level = 1;
    int currentHp = 1;
    int barMax = 1;
    int stepHp = 1;
    int rewardCells;
    int placedX = 1;
    int placedY = 1;
    float half;
    float pushedWidth = -1f;
    float pushedFlash = -1f;
    int pushedX = -1;
    int pushedY = -1;
    float pushedSeed = -1f;
    float pushedPlate = -1f;
    float pushedArmor = -1f;
    /// <summary>殻が弾いたときの白さ。0〜1。</summary>
    float plateWhite;
    /// <summary>はみ出して描く殻。下は Shell、左右は ShellLeft と ShellRight。</summary>
    SpriteRenderer[] shellParts;
    MaterialPropertyBlock shellBlock;
    /// <summary>顔の形と腐敗の模様を決める個体の種。出現のたびに変える。</summary>
    float faceSeed;
    Vector3 restScale = Vector3.one;
    Vector3 slotLocal;
    float stumbleClock;
    float stumblePeriod = 1.2f;
    float hold;
    float holdOnRecover;
    float lastParentY;
    bool stumbleReady;
    bool armorSides;
    const byte StumbleWalk = 0;
    const byte StumbleTrip = 1;
    const byte StumbleRecover = 2;
    byte stumblePhase;
    const float StumbleTripStart = 0.4f;
    const float StumbleTripEnd = 0.74f;
    const float StumbleMaxHold = 0.08f;

    public Vector3 PlacedScale => restScale;
    /// <summary>生成時のマス位置。つまずきで見た目がずれても、列の空き判定はここを使う。</summary>
    public Vector3 SlotLocalPosition => slotLocal;
    /// <summary>実際に置かれた横のマス数。入りきらないときは CellsX より小さく置かれることがある。</summary>
    public int PlacedCellsX => Mathf.Max(1, placedX);
    /// <summary>実際に置かれた縦のマス数。</summary>
    public int PlacedCellsY => Mathf.Max(1, placedY);
    public float PlacedLocalTop => slotLocal.y + restScale.y * 0.5f;

    /// <summary>
    /// 等速では落ちず、周期で止まって、それからマスへ落ち直す。
    /// 止まれる距離には上限がある。速い落下でも隣のマスには入らない。
    /// </summary>
    void TickStumble()
    {
        if (!stumbleReady || !IsAlive || breaking) return;

        var parent = transform.parent;
        if (parent == null) return;

        float parentY = parent.position.y;
        float dropped = lastParentY - parentY;
        lastParentY = parentY;
        if (dropped < 0f) dropped = 0f;
        else if (dropped > 0.5f) dropped = 0.5f;

        stumbleClock += Time.deltaTime;
        if (stumbleClock >= stumblePeriod)
            stumbleClock -= stumblePeriod;
        float u = stumblePeriod > 0.01f ? stumbleClock / stumblePeriod : 0f;

        if (u < StumbleTripStart)
        {
            stumblePhase = StumbleWalk;
            hold = 0f;
        }
        else if (u < StumbleTripEnd)
        {
            if (stumblePhase != StumbleTrip)
                stumblePhase = StumbleTrip;
            hold = Mathf.Min(StumbleMaxHold, hold + dropped);
        }
        else
        {
            if (stumblePhase != StumbleRecover)
            {
                stumblePhase = StumbleRecover;
                holdOnRecover = hold;
            }
            float recover = (u - StumbleTripEnd) / (1f - StumbleTripEnd);
            hold = holdOnRecover * (1f - recover);
        }

        var pos = slotLocal;
        pos.y += hold;
        transform.localPosition = pos;
    }

    void PlaySpawnPop()
    {
        var target = restScale;
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(target, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }
    Color fillColor = Color.white;
    Color underColor = new Color(0f, 0f, 0f, 0f);
    Color pushedFill = new Color(-1f, -1f, -1f, -1f);
    Color pushedUnder = new Color(-1f, -1f, -1f, -1f);
    Color baseOutline = Color.white;
    float flash;
    float reactTime = -1f;
    bool breaking;
    bool outlineReady;
    int spawnLevel = 1;
    int spawnHp = 1;
    int spawnBar = 1;
    float regenTimer;
    float healTime;
    float healPulse;
    float plateFlash = -1f;
    Vector3 regenMarkScale = Vector3.one;
    Color[] plateColors;

    const float HealPop = 0.28f;
    static readonly Color RegenHot = new Color(0.55f, 0.9f, 1f, 1f);

    public int CellsX => Mathf.Max(1, cellsX);
    public int CellsY => Mathf.Max(1, cellsY);
    public float Weight => Mathf.Max(0f, weight);
    public float HpScale => Mathf.Max(0f, hpScale);
    public int BossIndex => bossIndex;
    public bool Regenerates => regenDelay > 0f;
    public bool ArmorsBottom => armorBottom;
    /// <summary>膨張。倒すと周りを巻き込んで爆発する。</summary>
    public bool Bursts => burstRadius > 0f;
    /// <summary>爆発の半径（マス）。ゾンビ図鑑の研究で広がる。</summary>
    public float BurstRadius => Mathf.Max(0f, burstRadius) * (1f + BestiaryBonus.BloatRadius);
    /// <summary>爆発のダメージ。出現したときの総 HP × burstDamageRate。</summary>
    public int BurstDamage => Mathf.Max(1, Mathf.RoundToInt(((spawnLevel - 1) * stepHp + spawnHp) * Mathf.Max(0f, burstDamageRate)));
    public float RewardScale => Mathf.Max(0f, rewardScale);
    /// <summary>分裂。倒すと小さいゾンビに分かれる。</summary>
    public bool Splits => splitInto != null && splitCount > 0;
    public BlockPiece SplitInto => splitInto;
    public int SplitCount => Mathf.Max(0, splitCount);
    /// <summary>分裂した 1 体の総 HP。出現したときの総 HP × splitHpRate。</summary>
    /// <remarks>ゾンビ図鑑の研究で減る。</remarks>
    public int SplitHp => Mathf.Max(1, Mathf.RoundToInt(SpawnTotalHp * Mathf.Max(0f, splitHpRate) * (1f - BestiaryBonus.SplitHp)));
    public float SizeScale => Mathf.Clamp(sizeScale, 0.1f, 1f);

    /// <summary>分裂で出た子の向き。-1 が左の子、1 が右の子、0 は分裂の子ではない。縫い目を描く側が変わる。</summary>
    float splitSide;

    /// <summary>分裂で出た子の向きを決める。Spawn より前に呼ぶ。</summary>
    public void SetSplitSide(float side) => splitSide = side;
    /// <summary>幽霊。一定の間隔で半透明になり、その間はボールがすり抜ける。</summary>
    public bool Ghosts => ghostSolidTime > 0f;
    /// <summary>幽霊がすり抜けている最中。</summary>
    public bool Phased => phased;
    float GhostCycle => GhostSolidTime + Mathf.Max(0.1f, ghostPhaseTime);
    /// <summary>実体でいる秒数。ゾンビ図鑑の研究で延びる。</summary>
    float GhostSolidTime => ghostSolidTime * (1f + BestiaryBonus.GhostSolid);
    /// <summary>再生が始まるまでの秒数。ゾンビ図鑑の研究で延びる。</summary>
    float RegenWait => regenDelay * (1f + BestiaryBonus.RegenDelay);
    float ghostClock;
    float ghostFade;
    bool phased;
    /// <summary>ガラス。割れると中のゾンビが出てくる。</summary>
    public bool ReleasesContents => contents != null;
    public BlockPiece Contents => contents;
    /// <summary>資源を落とさない（ガラスなど）。</summary>
    public bool NoDrop => noDrop;
    /// <summary>宝箱。1 マスのゾンビの rewardScale 倍の AA を落とす。</summary>
    public bool IsChest => chest;
    /// <summary>甲羅ボス。開いた面にしかボールが効かない。</summary>
    public bool Shelled => shellTurnTime > 0f;
    /// <summary>再生ボス。種が溜まると大きく回復する。</summary>
    public bool BossHeals => bossHealTime > 0f;
    /// <summary>殻の開いた面。0 下、1 右、2 上、3 左。シェーダーの _ShellOpen と同じ。</summary>
    int shellOpen;
    float shellClock;
    float bossHealClock;
    float bossHealDamage;
    float bossHealStunLeft;
    public bool OverridesDrop => overrideDrop;
    public ResourceType DropResource => dropResource;
    /// <summary>出現したときの総 HP。</summary>
    public int SpawnTotalHp => Mathf.Max(1, (spawnLevel - 1) * stepHp + spawnHp);
    /// <summary>
    /// ゾンビ図鑑の id。プレハブで決めていなければ、ブロックの性質から決める。
    /// 雑魚：1 ゾンビ、2 装甲、3 再生、4 膨張、5 分裂、6 幽霊、7 ガラス、8 宝箱、9 石。ボス：101 大ゾンビ、102 甲羅、103 再生。
    /// </summary>
    public int BestiaryId
    {
        get
        {
            if (bestiaryId > 0) return bestiaryId;
            if (BossIndex >= 0)
            {
                if (Shelled) return 102;
                if (BossHeals) return 103;
                return 101;
            }
            if (solid) return 9;
            if (chest) return 8;
            if (ReleasesContents) return 7;
            if (Ghosts) return 6;
            if (Splits) return 5;
            if (Bursts) return 4;
            if (Regenerates) return 3;
            if (ArmorsBottom) return 2;
            return 1;
        }
    }
    /// <summary>出てからの秒数（図鑑のボスの記録）。</summary>
    public float AliveSeconds => Mathf.Max(0f, Time.time - spawnedAt);
    float spawnedAt;
    public bool IsAlive { get; private set; }
    public bool IsSolid => solid;
    /// <summary>生成されてから、一度もダメージを受けていない。</summary>
    public bool Unhurt { get; private set; }
    public int Level => level;
    public int Hp => currentHp;
    public int HpMax => barMax;
    /// <summary>生成時の総HPに対する、いま残っている総HP。</summary>
    public float HpRatio
    {
        get
        {
            int total = (spawnLevel - 1) * stepHp + spawnHp;
            if (total <= 0) return 1f;
            int left = (level - 1) * stepHp + currentHp;
            return Mathf.Clamp01((float)left / total);
        }
    }
    public float Bottom => transform.position.y - half;
    /// <summary>ブロックの中心。ボールスキルの発生位置はここを基準にする。</summary>
    public Vector2 Center => transform.position;

    void OnEnable()
    {
        if (!Application.isPlaying)
            ApplyEditorPreview();
    }

    void OnValidate()
    {
        cellsX = Mathf.Max(1, cellsX);
        cellsY = Mathf.Max(1, cellsY);
        if (!Application.isPlaying)
            ApplyEditorPreview();
    }

    void ApplyEditorPreview()
    {
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();
        if (sprite == null) return;

        transform.localScale = new Vector3(CellsX, CellsY, 1f);
        if (!solid)
        {
            level = 1;
            currentHp = 1;
            barMax = 1;
            stepHp = 1;
        }
        faceSeed = Mathf.Repeat(GetInstanceID() * 0.6180339f, 1f);
        CacheOutline();
        RefreshColor();
    }

    void Update()
    {
        TickStumble();
        TickPlate();
        TickRegen();
        TickGhost();
        TickShell();
        TickBossHeal();
        if (reactTime < 0f) return;

        reactTime += Time.deltaTime;
        if (breaking)
        {
            transform.localScale = restScale * HitScale;
            flash = 1f;
            InvalidateFill();
            PushFill();
            if (reactTime < HitHold) return;

            reactTime = -1f;
            Destroy(gameObject);
            return;
        }

        float returnEnd = HitHold + HitReturn;
        float amount = 1f;
        if (reactTime >= returnEnd)
            amount = 0f;
        else if (reactTime > HitHold)
        {
            float t = (reactTime - HitHold) / HitReturn;
            amount = 1f - t * t;
        }

        transform.localScale = Vector3.LerpUnclamped(restScale, restScale * HitScale, amount);

        float fadeEnd = HitHold + FlashFade;
        if (reactTime <= HitHold)
            flash = 1f;
        else if (reactTime >= fadeEnd)
            flash = 0f;
        else
            flash = 1f - (reactTime - HitHold) / FlashFade;

        InvalidateFill();
        PushFill();

        if (reactTime < Mathf.Max(returnEnd, fadeEnd)) return;

        reactTime = -1f;
        flash = 0f;
        transform.localScale = restScale;
        InvalidateFill();
        PushFill();
    }

    void OnWillRenderObject()
    {
        PushFill();
    }

    public void Spawn(BlockSession owner, Vector2 worldSize, int startLevel, int hp, int bar, int step, int spanX, int spanY)
    {
        session = owner;
        placedX = Mathf.Max(1, spanX);
        placedY = Mathf.Max(1, spanY);
        rewardCells = chest ? 1 : placedX * placedY;
        level = solid ? 1 : Mathf.Max(1, startLevel);
        barMax = solid ? 1 : Mathf.Max(1, bar);
        stepHp = solid ? 1 : Mathf.Max(1, step);
        currentHp = solid ? 1 : Mathf.Clamp(hp, 0, barMax);
        spawnLevel = level;
        spawnHp = currentHp;
        spawnBar = barMax;
        Unhurt = !solid;
        regenTimer = 0f;
        healTime = 0f;
        healPulse = 0f;
        // 幽霊は出現ごとに周期をずらし、全員が同時に消えないようにする。
        ghostClock = Ghosts ? Random.Range(0f, GhostSolidTime) : 0f;
        ghostFade = 0f;
        phased = false;
        shellOpen = 0;
        shellClock = 0f;
        bossHealClock = 0f;
        bossHealDamage = 0f;
        bossHealStunLeft = 0f;
        half = worldSize.y * 0.5f;
        restScale = transform.localScale;
        slotLocal = transform.localPosition;
        stumblePeriod = Random.Range(0.95f, 1.55f);
        faceSeed = Random.value;
        spawnedAt = Time.time;
        // 倒せない石は、画面に出たときに図鑑で発見とする。
        if (solid)
            BestiaryRuntime.NotifySeen(BestiaryId);
        stumbleClock = Random.Range(0f, stumblePeriod);
        hold = 0f;
        stumblePhase = StumbleWalk;
        lastParentY = transform.parent != null ? transform.parent.position.y : transform.position.y;
        // 宝箱と壊せないブロックは揺れずに下りてくる。
        stumbleReady = !chest && !solid;
        PlaySpawnPop();
        reactTime = -1f;
        breaking = false;
        flash = 0f;
        IsAlive = true;

        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();
        if (sprite == null)
        {
            Debug.LogWarning("BlockPiece に SpriteRenderer がありません。", this);
            sprite = gameObject.AddComponent<SpriteRenderer>();
        }
        if (sprite.sprite == null)
            sprite.sprite = BlockSession.SquareSprite;
        sprite.color = Color.white;
        sprite.sortingOrder = 0;

        if (!solid && !HasLevelFill())
        {
            var material = SharedFill();
            if (material != null)
                sprite.sharedMaterial = material;
        }

        // 当たり判定はプレハブのものを使う。丸い敵は CircleCollider2D、それ以外は四角。
        var collider = GetComponent<Collider2D>();
        if (collider == null)
            collider = gameObject.AddComponent<BoxCollider2D>();
        if (collider is BoxCollider2D box)
            box.size = Vector2.one;
        else if (collider is CircleCollider2D circle)
            circle.radius = 0.5f;
        collider.enabled = true;
        gameObject.layer = BlockSession.BlockLayer;
        CacheOutline();
        RefreshColor();
        ChooseArmorFace();
        BindTraitMarks();
    }

    /// <summary>当たった位置。下シールドは下端、左右シールドは左右の端をダメージにしない。ballKind は BlockBallKind。不明なら -1。</summary>
    public bool HitFrom(int damage, Vector2 worldPoint, int ballKind = -1, bool critical = false, bool fullStack = false)
    {
        // ボールが直接当たった回数を、石の図鑑で数える。
        if (solid && IsAlive)
            BestiaryRuntime.NotifyHit(BestiaryId);
        if (RejectsArmor(worldPoint))
        {
            PlayPlateDeflect(worldPoint);
            // ゾンビ図鑑の研究：殻に当たっても、ダメージの一部が本体に通る。
            float pierce = BestiaryBonus.ArmorPierce;
            if (pierce > 0f)
                ApplyHit(Mathf.Max(1, Mathf.RoundToInt(damage * pierce)), ballKind, critical, worldPoint, fullStack);
            return false;
        }

        ApplyHit(damage, ballKind, critical, worldPoint, fullStack);
        return true;
    }

    public void Hit(int damage, int ballKind = -1, bool critical = false, bool fullStack = false)
    {
        ApplyHit(damage, ballKind, critical, Center, fullStack);
    }

    void ApplyHit(int damage, int ballKind, bool critical, Vector2 popupPoint, bool fullStack)
    {
        if (!IsAlive || solid) return;
        if (session != null && !session.IsOnCamera(this)) return;
        // すり抜けている幽霊には、レーザーしか当たらない。
        if (phased && !HitsGhost(ballKind)) return;

        regenTimer = 0f;
        float scale = SkillTreeBonus.StrikeScale(this, fullStack);
        Unhurt = false;
        int applied = Mathf.Max(1, SkillTreeBonus.RoundDamage(Mathf.Max(1, damage) * scale));
        int left = applied;
        while (left > 0 && level > 0)
        {
            int pool = Mathf.Max(0, currentHp);
            if (left < pool)
            {
                currentHp = pool - left;
                break;
            }

            left -= pool;
            level--;
            if (level <= 0)
            {
                currentHp = 0;
                break;
            }

            barMax = Mathf.Max(1, stepHp);
            currentHp = barMax;
        }

        NoteBossHealDamage(applied);
        GameEvent.InGame.PublishDamageDealt(applied, ballKind);
        UI_DamageNumberPool.Show(popupPoint, applied, ballKind, critical);
        if (level > 0)
        {
            // ガラスは専用の音。会心でもガラスの音にする。
            if (ReleasesContents)
                SoundManager.SE(SEType.GlassHit);
            else
                SoundManager.SE(critical ? SEType.BlockHitCritical : SEType.BlockHit);
            RefreshColor();
            PlayReact(false);
            return;
        }

        IsAlive = false;
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;

        if (session != null)
            session.GrantBreakResource(this, transform.position, rewardCells);

        if (!noDrop)
            MilestoneRuntime.NotifyBlockBroken();
        BestiaryRuntime.NotifyKill(this);
        session.NotifyBroken(this);
        if (BossIndex >= 0 && session != null && session.BeginBossDefeat(this))
            return;

        SoundManager.SE(SEType.BlockBreak);
        if (Bursts && session != null)
            session.QueueBurst(this);
        if (Splits && session != null)
            session.SpawnSplit(this);
        if (ReleasesContents && session != null)
            session.ReleaseContents(this);
        if (EffectManager.Inst != null)
        {
            EffectManager.Inst.Rent(EffectType.BlockBreak, transform.position);
            // ガラスと宝箱は血を残さない。
            if (!ReleasesContents && !chest)
                EffectManager.Inst.Rent(EffectType.BloodStain, transform.position);
        }
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Small);
        PlayReact(true);
    }

    /// <summary>破壊直前の見た目で止める。時間はセッション側が止める。</summary>
    public void HoldBreak()
    {
        reactTime = -1f;
        breaking = false;
        flash = 1f;
        transform.DOKill();
        transform.localScale = restScale * HitScale;
        InvalidateFill();
        PushFill();
    }

    public void FinishBreak()
    {
        if (EffectManager.Inst != null)
        {
            EffectManager.Inst.Rent(EffectType.BlockBreak, transform.position);
            EffectManager.Inst.Rent(EffectType.BloodStain, transform.position);
        }
        PlayReact(true);
    }

    void PlayReact(bool willBreak)
    {
        if (willBreak)
            breaking = true;

        reactTime = 0f;
        flash = 1f;
        transform.DOKill();
        transform.localScale = restScale * HitScale;
        InvalidateFill();
        PushFill();
    }

    void RefreshColor()
    {
        if (sprite == null) return;

        // 色は敵ごとに固定。塗る量は、出現したときの総 HP に対する残りの総 HP の割合。
        float ratio = solid ? 1f : HpRatio;
        sprite.color = new Color(1f, 1f, 1f, ratio);

        var nextFill = bodyColor;
        nextFill.a = 1f;
        var nextUnder = new Color(0f, 0f, 0f, 0f);
        if (nextFill != fillColor || nextUnder != underColor)
        {
            fillColor = nextFill;
            underColor = nextUnder;
            InvalidateFill();
        }
        PushFill();
    }

    bool HasLevelFill()
    {
        var material = sprite != null ? sprite.sharedMaterial : null;
        var shader = material != null ? material.shader : null;
        if (shader == null) return false;
        var name = shader.name;
        return name == LevelShader || name == BossShader || name == StoneShader;
    }

    void PushFill()
    {
        if (sprite == null || !HasLevelFill()) return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        int spanX = Mathf.Max(1, Application.isPlaying ? placedX : CellsX);
        int spanY = Mathf.Max(1, Application.isPlaying ? placedY : CellsY);
        if (!outlineReady)
            CacheOutline();

        float width = Mathf.Max(0.0001f, sprite.sharedMaterial.GetFloat(OutlineWidthId));
        bool liveOutline = (regenDelay > 0f || Ghosts || Shelled || BossHeals) && IsAlive;
        if (!liveOutline && propertyBlock != null && width == pushedWidth && spanX == pushedX && spanY == pushedY
            && flash == pushedFlash && fillColor == pushedFill && underColor == pushedUnder && faceSeed == pushedSeed
            && plateWhite == pushedPlate && ArmorSide() == pushedArmor)
            return;

        pushedWidth = width;
        pushedFlash = flash;
        pushedFill = fillColor;
        pushedUnder = underColor;
        pushedX = spanX;
        pushedY = spanY;
        pushedSeed = faceSeed;
        pushedPlate = plateWhite;
        pushedArmor = ArmorSide();
        propertyBlock.Clear();
        propertyBlock.SetFloat(OutlineWidthId, width);
        // 小さいゾンビは 1 マスより小さいので、ドットの格子もその大きさに合わせる。
        float cellsWide = spanX * SizeScale;
        float cellsHigh = spanY * SizeScale;
        propertyBlock.SetVector(OutlineWidthXYId, new Vector4(width / cellsWide, width / cellsHigh, 0f, 0f));
        propertyBlock.SetVector(CellSpanId, new Vector4(cellsWide, cellsHigh, 0f, 0f));
        // 壊せないブロックはレベルの色を使わず、マテリアルの色のままにする。
        if (!solid)
        {
            propertyBlock.SetColor(FillColorId, fillColor);
            propertyBlock.SetColor(UnderColorId, underColor);
        }
        propertyBlock.SetColor(OutlineColorId, TraitOutline());
        propertyBlock.SetFloat(FlashId, flash);
        // 壊せないブロックと宝箱には顔を描かない。
        propertyBlock.SetFloat(ZombieId, solid || chest ? 0f : 1f);
        propertyBlock.SetFloat(SeedId, faceSeed);
        if (armorBottom)
            propertyBlock.SetFloat(ArmorSideId, ArmorSide());
        if (splitSide != 0f)
            propertyBlock.SetFloat(SplitSideId, splitSide);
        if (Ghosts)
            propertyBlock.SetFloat(GhostFadeId, ghostFade);
        if (Shelled)
        {
            propertyBlock.SetFloat(ShellOpenId, shellOpen);
            propertyBlock.SetFloat(ShellWarnId, ShellWarn());
            propertyBlock.SetFloat(ShellHitId, plateWhite);
        }
        if (BossHeals)
            propertyBlock.SetFloat(PodCrackId, PodCrack());
        if (ReleasesContents)
            propertyBlock.SetFloat(GlassCrackId, 1f - HpRatio);
        if (chest)
            propertyBlock.SetFloat(ChestDamageId, 1f - HpRatio);
        if (regenDelay > 0f || BossHeals)
        {
            propertyBlock.SetFloat(RegenGlowId, RegenGlow());
            propertyBlock.SetFloat(RegenChargeId, RegenCharge());
            propertyBlock.SetFloat(HealPulseId, healPulse);
        }
        sprite.SetPropertyBlock(propertyBlock);
        if (armorBottom)
            PushShells(spanX, spanY);
    }

    /// <summary>
    /// 殻をドットの格子に合わせて置き、形と光をシェーダーに渡す。
    /// 殻のドットは本体の 1/ShellDetail の大きさ。数えるのは殻のドット。
    /// 下の殻は左右に 1 ドット、下に 2 ドットはみ出す。左右の殻は外へ 1 ドット、内側へ ShellInnerCols ドット。
    /// </summary>
    void PushShells(int spanX, int spanY)
    {
        if (shellParts == null)
            shellParts = FindShells();
        if (shellParts.Length == 0) return;
        if (shellBlock == null)
            shellBlock = new MaterialPropertyBlock();

        float dot = sprite.sharedMaterial.HasProperty(DotScaleId) ? sprite.sharedMaterial.GetFloat(DotScaleId) : 8f;
        dot *= ShellDetail;
        int tx = Mathf.Max(1, Mathf.RoundToInt(spanX * dot));
        int ty = Mathf.Max(1, Mathf.RoundToInt(spanY * dot));
        float ux = 1f / tx;
        float uy = 1f / ty;
        float white = Mathf.Max(plateWhite, flash);

        for (int i = 0; i < shellParts.Length; i++)
        {
            var part = shellParts[i];
            if (part == null) continue;

            float mode;
            int dotsX;
            int dotsY;
            float th = 0f;
            float left;
            float bottom;
            if (part.name == "Shell")
            {
                mode = 1f;
                th = Mathf.Ceil(ty * 0.3f) + 2f;
                dotsX = tx + 2;
                dotsY = (int)th + 1;
                left = -0.5f - ux;
                bottom = -0.5f - 2f * uy;
            }
            else
            {
                bool isLeft = part.name == "ShellLeft";
                mode = isLeft ? 2f : 3f;
                dotsX = ShellInnerCols(tx) + 1;
                dotsY = ty;
                left = isLeft ? -0.5f - ux : 0.5f + ux - dotsX * ux;
                bottom = -0.5f;
            }

            float w = dotsX * ux;
            float h = dotsY * uy;
            // 弾いた瞬間は、殻を内側へ 1 ドット押し込む。
            Vector2 recoil = mode < 1.5f ? new Vector2(0f, uy) : new Vector2(mode < 2.5f ? ux : -ux, 0f);
            recoil *= plateWhite;
            var t = part.transform;
            t.localPosition = new Vector3(left + w * 0.5f + recoil.x, bottom + h * 0.5f + recoil.y, 0f);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(w, h, 1f);

            shellBlock.Clear();
            shellBlock.SetVector(ShellDotsId, new Vector4(dotsX, dotsY, 0f, 0f));
            shellBlock.SetFloat(ShellThId, th);
            shellBlock.SetFloat(ShellModeId, mode);
            shellBlock.SetFloat(ShellFlashId, white);
            part.SetPropertyBlock(shellBlock);
        }
    }

    /// <summary>左右の殻がブロックの内側に入り込む列数（殻のドット）。本体のドットに直すと、シェーダーの ShellInnerCols と同じになる。</summary>
    static int ShellInnerCols(int totalX)
    {
        return Mathf.Max(2, Mathf.FloorToInt(totalX * 0.3f));
    }

    SpriteRenderer[] FindShells()
    {
        var found = GetComponentsInChildren<SpriteRenderer>(true);
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        for (int i = 0; i < found.Length; i++)
        {
            var name = found[i].name;
            if (name == "Shell" || name == "ShellLeft" || name == "ShellRight")
                list.Add(found[i]);
        }
        return list.ToArray();
    }

    void InvalidateFill()
    {
        pushedWidth = -1f;
        pushedFlash = -1f;
        pushedFill = new Color(-1f, -1f, -1f, -1f);
        pushedUnder = new Color(-1f, -1f, -1f, -1f);
    }

    void CacheOutline()
    {
        baseOutline = Color.white;
        if (sprite != null && sprite.sharedMaterial != null && sprite.sharedMaterial.HasProperty(OutlineColorId))
            baseOutline = sprite.sharedMaterial.GetColor(OutlineColorId);
        outlineReady = true;
    }

    Color TraitOutline()
    {
        Color outline = Color.Lerp(baseOutline, Color.white, flash);
        if (regenDelay <= 0f && !BossHeals) return outline;

        float charge = RegenCharge();
        float speed = MissingDurability() ? Mathf.Lerp(2.4f, 9f, charge) : 1.6f;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
        float amount = MissingDurability() ? Mathf.Lerp(0.25f, 1f, charge) * wave : 0.3f * wave;
        return Color.Lerp(outline, RegenHot, Mathf.Max(amount, healPulse));
    }

    /// <summary>額の種の明るさ。満タンのときはゆっくり、回復を溜めている間は速く強く脈打つ。</summary>
    float RegenGlow()
    {
        if ((regenDelay <= 0f && !BossHeals) || !IsAlive) return 0f;

        float charge = RegenCharge();
        bool missing = MissingDurability();
        float speed = missing ? Mathf.Lerp(2.4f, 9f, charge) : 1.6f;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
        if (!missing) return 0.2f + 0.2f * wave;
        return Mathf.Lerp(0.35f, 1f, charge) * Mathf.Lerp(0.7f, 1f, wave);
    }

    /// <summary>シェーダーに渡す殻の向き。装甲なし 0、下 1、左右 2。</summary>
    float ArmorSide()
    {
        if (!armorBottom || solid) return 0f;
        return armorSides ? 2f : 1f;
    }

    bool RejectsArmor(Vector2 worldPoint)
    {
        if (Shelled && IsAlive)
            return ShellFace(worldPoint) != shellOpen;
        if (!armorBottom || !IsAlive || solid) return false;

        Vector2 local = transform.InverseTransformPoint(worldPoint);
        if (armorSides)
            return local.x < -0.2f || local.x > 0.2f;
        return local.y < -0.2f;
    }

    void ChooseArmorFace()
    {
        armorSides = false;
        if (!armorBottom) return;

        var under = FindChild("obj_under");
        var side = FindChild("obj_side");
        armorSides = side != null && Random.value < 0.5f;
        if (under != null)
            under.gameObject.SetActive(!armorSides);
        if (side != null)
            side.gameObject.SetActive(armorSides);

        var active = armorSides ? side : under;
        if (active == null) return;

        plateMarks = active.GetComponentsInChildren<SpriteRenderer>(true);
    }

    void PlayPlateDeflect(Vector2 worldPoint)
    {
        plateFlash = 0.16f;
        SoundManager.SE(SEType.ArmorDeflect);
        if (EffectManager.Inst == null) return;

        // 殻の外向きに火花を散らす。下の殻は下へ、左右の殻は当たった側へ。
        Vector2 local = transform.InverseTransformPoint(worldPoint);
        Vector3 outward = armorSides
            ? transform.right * (local.x < 0f ? -1f : 1f)
            : -transform.up;
        if (Shelled)
            outward = ((Vector3)worldPoint - transform.position).normalized;
        var rotation = Quaternion.FromToRotation(Vector3.up, outward);
        EffectManager.Inst.Rent(EffectType.ShellSpark, worldPoint, rotation);
    }

    void TickPlate()
    {
        if (plateFlash < 0f) return;

        plateFlash -= Time.deltaTime;
        float white = plateFlash <= 0f ? 0f : Mathf.Clamp01(plateFlash / 0.1f);
        plateWhite = white;
        // 殻の光はシェーダーで出す。板のスプライトが残っていれば、そちらも光らせる。
        int count = plateMarks != null && plateColors != null ? Mathf.Min(plateMarks.Length, plateColors.Length) : 0;
        for (int i = 0; i < count; i++)
        {
            if (plateMarks[i] == null) continue;
            plateMarks[i].color = Color.Lerp(plateColors[i], Color.white, white);
        }
        if (plateFlash <= 0f)
            plateFlash = -1f;
    }

    void TickRegen()
    {
        if (healTime > 0f)
        {
            healTime -= Time.deltaTime;
            healPulse = Mathf.Clamp01(healTime / HealPop);
            if (reactTime < 0f && !DOTween.IsTweening(transform))
                transform.localScale = restScale * (1f + 0.08f * healPulse);
            if (healTime <= 0f)
            {
                healTime = 0f;
                healPulse = 0f;
                if (reactTime < 0f && !DOTween.IsTweening(transform))
                    transform.localScale = restScale;
            }
            InvalidateFill();
        }

        PulseRegenMark();
        if (regenDelay <= 0f || !IsAlive || solid || breaking) return;
        if (!MissingDurability())
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer < RegenWait) return;

        regenTimer = 0f;
        RestoreStep();
    }

    float RegenCharge()
    {
        if (BossHeals)
            return MissingDurability() ? Mathf.Clamp01(bossHealClock / bossHealTime) : 0f;
        if (regenDelay <= 0f || !MissingDurability()) return 0f;
        return Mathf.Clamp01(regenTimer / RegenWait);
    }

    bool MissingDurability()
    {
        return level < spawnLevel || currentHp < spawnHp;
    }

    void RestoreStep()
    {
        if (level < spawnLevel)
        {
            level++;
            if (level >= spawnLevel)
            {
                level = spawnLevel;
                barMax = spawnBar;
                currentHp = spawnHp;
            }
            else
            {
                barMax = Mathf.Max(1, stepHp);
                currentHp = barMax;
            }
        }
        else if (currentHp < spawnHp)
        {
            currentHp = spawnHp;
            barMax = spawnBar;
        }
        else
            return;

        RefreshColor();
        healTime = HealPop;
        healPulse = 1f;
        SoundManager.SE(SEType.RegenHeal);
        InvalidateFill();
        PushFill();
    }

    void PulseRegenMark()
    {
        if (regenMark == null) return;

        float charge = RegenCharge();
        float speed = MissingDurability() ? Mathf.Lerp(2.4f, 9f, charge) : 1.6f;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
        float pop = healPulse * 0.55f;
        float scale = (Mathf.Lerp(0.92f, 1.22f, charge) * Mathf.Lerp(0.94f, 1.08f, wave)) + pop;
        regenMark.localScale = regenMarkScale * scale;
        if (regenDelay > 0f && IsAlive)
            InvalidateFill();
    }

    /// <summary>
    /// 幽霊の周期。実体 → すり抜け → 実体 をくり返す。すり抜けの間は当たり判定を切る。
    /// 実体に戻るとき、ボールが中にいたら、抜けるまで待つ（ボールが埋まらないように）。
    /// </summary>
    void TickGhost()
    {
        if (!Ghosts || !IsAlive || breaking) return;

        float dt = Time.deltaTime;
        float cycle = GhostCycle;
        ghostClock += dt;
        if (ghostClock >= cycle)
        {
            if (phased && BallInside())
                ghostClock = cycle;
            else
            {
                ghostClock -= cycle;
                if (phased)
                    SetPhased(false);
            }
        }
        else if (!phased && ghostClock >= GhostSolidTime)
            SetPhased(true);

        bool fading = ghostClock >= GhostSolidTime - GhostLead && ghostClock < cycle - GhostLead;
        float target = phased && ghostClock >= cycle ? 1f : (fading ? 1f : 0f);
        ghostFade = Mathf.MoveTowards(ghostFade, target, dt / GhostLead);
        InvalidateFill();
    }

    void SetPhased(bool value)
    {
        phased = value;
        var collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = !value;
    }

    bool BallInside()
    {
        if (sprite == null) return false;
        var bounds = sprite.bounds;
        return Physics2D.OverlapBox(bounds.center, bounds.size, 0f, 1 << BlockSession.BallLayer) != null;
    }

    static bool HitsGhost(int ballKind)
    {
        return ballKind == (int)BlockBallKind.Laser || ballKind == (int)BlockBallKind.LaserRow
            || ballKind == (int)BlockBallKind.Cross || ballKind == (int)BlockBallKind.AutoBeam;
    }

    /// <summary>
    /// 当たった位置がどの面か。0 下、1 右、2 上、3 左。
    /// ブロックの中心から見て、縦横を同じ長さにならした向きで決める（シェーダーの殻の分け方と同じ）。
    /// </summary>
    int ShellFace(Vector2 worldPoint)
    {
        // 出現の弾みで拡縮していても向きがぶれないよう、置いたときの大きさで割る。
        Vector2 local = worldPoint - (Vector2)transform.position;
        local.x /= Mathf.Max(0.001f, Mathf.Abs(restScale.x));
        local.y /= Mathf.Max(0.001f, Mathf.Abs(restScale.y));
        if (Mathf.Abs(local.y) > Mathf.Abs(local.x))
            return local.y < 0f ? 0 : 2;
        return local.x > 0f ? 1 : 3;
    }

    /// <summary>甲羅ボスの殻を回す。回る直前は殻が点滅する。</summary>
    void TickShell()
    {
        if (!Shelled || !IsAlive || breaking) return;

        shellClock += Time.deltaTime;
        if (shellClock >= shellTurnTime)
        {
            shellClock -= shellTurnTime;
            shellOpen = (shellOpen + 1) % 4;
            SoundManager.SE(SEType.ArmorDeflect);
        }
        InvalidateFill();
    }

    float ShellWarn()
    {
        float left = shellTurnTime - shellClock;
        return left < ShellWarnTime ? 1f - left / ShellWarnTime : 0f;
    }

    /// <summary>
    /// 再生ボスの種。HP が減っていると溜まり、溜まりきると出現時の総 HP × bossHealRate を回復する。
    /// 溜まる間に一定のダメージを受けると種が割れ、しばらく溜めない。
    /// </summary>
    void TickBossHeal()
    {
        if (!BossHeals || !IsAlive || breaking) return;

        InvalidateFill();
        if (bossHealStunLeft > 0f)
        {
            bossHealStunLeft -= Time.deltaTime;
            return;
        }
        if (!MissingDurability())
        {
            bossHealClock = 0f;
            bossHealDamage = 0f;
            return;
        }

        bossHealClock += Time.deltaTime;
        if (bossHealClock < bossHealTime) return;

        bossHealClock = 0f;
        bossHealDamage = 0f;
        int total = (level - 1) * stepHp + currentHp;
        SetTotalHp(total + Mathf.RoundToInt(SpawnTotalHp * Mathf.Max(0f, bossHealRate)));
        RefreshColor();
        healTime = HealPop;
        healPulse = 1f;
        SoundManager.SE(SEType.RegenHeal);
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Small);
        PushFill();
    }

    void NoteBossHealDamage(int applied)
    {
        if (!BossHeals || bossHealStunLeft > 0f || bossHealClock <= 0f) return;

        bossHealDamage += applied;
        if (bossHealDamage < SpawnTotalHp * Mathf.Max(0.001f, bossHealBreakRate)) return;

        // 種が割れた。回復の溜めを捨てて、しばらく溜めない。
        bossHealClock = 0f;
        bossHealDamage = 0f;
        bossHealStunLeft = Mathf.Max(0f, bossHealStun);
        plateFlash = 0.16f;
        SoundManager.SE(SEType.ArmorDeflect);
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Small);
        if (EffectManager.Inst != null)
            EffectManager.Inst.Rent(EffectType.ShellSpark, transform.position + transform.up * Mathf.Abs(transform.lossyScale.y) * 0.2f, Quaternion.identity);
    }

    /// <summary>種のひび。割れるまでに与えたダメージの割合。割れた直後は 1 から消えていく。</summary>
    float PodCrack()
    {
        if (bossHealStunLeft > 0f)
            return Mathf.Clamp01(bossHealStunLeft / Mathf.Max(0.01f, bossHealStun));
        float need = SpawnTotalHp * Mathf.Max(0.001f, bossHealBreakRate);
        return Mathf.Clamp01(bossHealDamage / need);
    }

    /// <summary>残りの総 HP を決め直す。出現したときの総 HP を超えない。</summary>
    void SetTotalHp(int total)
    {
        int spawnTotal = SpawnTotalHp;
        total = Mathf.Clamp(total, 1, spawnTotal);
        int below = (spawnLevel - 1) * stepHp;
        if (total > below)
        {
            level = spawnLevel;
            barMax = spawnBar;
            currentHp = total - below;
            return;
        }

        level = Mathf.Max(1, Mathf.CeilToInt(total / (float)stepHp));
        barMax = Mathf.Max(1, stepHp);
        currentHp = total - (level - 1) * stepHp;
    }

    void BindTraitMarks()
    {
        if (armorBottom && !HasLiveMarks(plateMarks))
            plateMarks = ChildMarks();

        if (HasLiveMarks(plateMarks))
        {
            plateColors = new Color[plateMarks.Length];
            for (int i = 0; i < plateMarks.Length; i++)
                plateColors[i] = plateMarks[i] != null ? plateMarks[i].color : Color.white;
        }

        if (regenMark == null && regenDelay > 0f)
            regenMark = FindChild("Regen");

        if (regenMark != null)
            regenMarkScale = regenMark.localScale;
    }

    static bool HasLiveMarks(SpriteRenderer[] marks)
    {
        if (marks == null) return false;
        for (int i = 0; i < marks.Length; i++)
            if (marks[i] != null) return true;
        return false;
    }

    SpriteRenderer[] ChildMarks()
    {
        var found = GetComponentsInChildren<SpriteRenderer>(true);
        int count = 0;
        for (int i = 0; i < found.Length; i++)
            if (found[i] != null && found[i] != sprite)
                count++;
        if (count == 0) return null;

        var marks = new SpriteRenderer[count];
        int n = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || found[i] == sprite) continue;
            marks[n++] = found[i];
        }
        return marks;
    }

    Transform FindChild(string childName)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child != null && child != transform && child.name == childName)
                return child;
        }
        return null;
    }

    static Material SharedFill()
    {
        if (sharedFill != null) return sharedFill;

        var shader = Shader.Find(LevelShader);
        if (shader == null)
        {
            Debug.LogWarning("BlockLevelFill が見つかりません。");
            return null;
        }

        sharedFill = new Material(shader) { name = "BlockLevelFill" };
        return sharedFill;
    }
}
