using DG.Tweening;
using UnityEngine;

/// <summary>
/// ブロック1個。プレハブのマス数、色、HP倍率、壊れない設定を使う。
/// 見た目は BlockLevelFill。ボスのプレハブは BlockBossFill を付けたまま使う。
/// 現在レベルの色を下から塗り、空きは一つ下の色。レベル1の空きは透過。
/// 輪郭の太さはスケールが変わっても同じにする。
/// 被弾で白く光り、一瞬大きくなる。破壊時も同じ演出を出し、その場に破壊エフェクトを出す。
/// 装甲は下端か左右の端だけを受けない。出現のたびにどちらかになる。再生はヒットが途切れると失った段を戻す。
/// </summary>
public class BlockPiece : MonoBehaviour
{
    const string LevelShader = "ShapeBuster/BlockLevelFill";
    const string BossShader = "ShapeBuster/BlockBossFill";
    const float HitHold = 0.05f;
    const float HitReturn = 0.08f;
    const float HitScale = 1.18f;
    const float FlashFade = 0.09f;

    static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    static readonly int UnderColorId = Shader.PropertyToID("_UnderColor");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    static readonly int OutlineWidthXYId = Shader.PropertyToID("_OutlineWidthXY");
    static readonly int CellSpanId = Shader.PropertyToID("_CellSpan");
    static readonly int FlashId = Shader.PropertyToID("_Flash");

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
    [Tooltip("0以上ならボス。ステージSOの bossIndex と一致させる。通常ブロックは -1")]
    [SerializeField] int bossIndex = -1;
    [Tooltip("下の面から来た弾はダメージにならない。爆発、レーザー、毒は通る。")]
    [SerializeField] bool armorBottom;
    [Tooltip("ヒットが途切れてから、失った段を1つ戻すまでの秒数。0 なら再生しない。")]
    [SerializeField] float regenDelay;
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
    static readonly Color RegenHot = new Color(0.72f, 1f, 0.78f, 1f);

    public int CellsX => Mathf.Max(1, cellsX);
    public int CellsY => Mathf.Max(1, cellsY);
    public float Weight => Mathf.Max(0f, weight);
    public float HpScale => Mathf.Max(0f, hpScale);
    public int BossIndex => bossIndex;
    public bool Regenerates => regenDelay > 0f;
    public bool ArmorsBottom => armorBottom;
    /// <summary>マイルストーンのブロック種別。通常 0、装甲 1、再生 2、ボス 3。</summary>
    public int BreakKey
    {
        get
        {
            if (BossIndex >= 0) return 3;
            if (ArmorsBottom) return 1;
            if (Regenerates) return 2;
            return 0;
        }
    }
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
        CacheOutline();
        RefreshColor();
    }

    void Update()
    {
        TickStumble();
        TickPlate();
        TickRegen();
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
        rewardCells = placedX * placedY;
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
        half = worldSize.y * 0.5f;
        restScale = transform.localScale;
        slotLocal = transform.localPosition;
        stumblePeriod = Random.Range(0.95f, 1.55f);
        stumbleClock = Random.Range(0f, stumblePeriod);
        hold = 0f;
        stumblePhase = StumbleWalk;
        lastParentY = transform.parent != null ? transform.parent.position.y : transform.position.y;
        stumbleReady = true;
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

        var collider = GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
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
        if (RejectsArmor(worldPoint))
        {
            PlayPlateDeflect();
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

        GameEvent.InGame.PublishDamageDealt(applied, ballKind);
        UI_DamageNumberPool.Show(popupPoint, applied, ballKind, critical);
        if (level > 0)
        {
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

        MilestoneRuntime.NotifyBlockBroken(BreakKey);
        session.NotifyBroken(this);
        if (BossIndex >= 0 && session != null && session.BeginBossDefeat(this))
            return;

        if (EffectManager.Inst != null)
        {
            EffectManager.Inst.Rent(EffectType.BlockBreak, transform.position);
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

        float ratio = 1f;
        if (!solid && barMax > 0)
            ratio = Mathf.Clamp01((float)currentHp / barMax);
        sprite.color = new Color(1f, 1f, 1f, ratio);

        var nextFill = SO_BlockLevel.ColorFor(Mathf.Max(1, level));
        var nextUnder = SO_BlockLevel.ColorFor(level - 1);
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
        return name == LevelShader || name == BossShader;
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
        bool liveOutline = regenDelay > 0f && IsAlive;
        if (!liveOutline && propertyBlock != null && width == pushedWidth && spanX == pushedX && spanY == pushedY
            && flash == pushedFlash && fillColor == pushedFill && underColor == pushedUnder)
            return;

        pushedWidth = width;
        pushedFlash = flash;
        pushedFill = fillColor;
        pushedUnder = underColor;
        pushedX = spanX;
        pushedY = spanY;
        propertyBlock.Clear();
        propertyBlock.SetFloat(OutlineWidthId, width);
        propertyBlock.SetVector(OutlineWidthXYId, new Vector4(width / spanX, width / spanY, 0f, 0f));
        propertyBlock.SetVector(CellSpanId, new Vector4(spanX, spanY, 0f, 0f));
        propertyBlock.SetColor(FillColorId, fillColor);
        propertyBlock.SetColor(UnderColorId, underColor);
        propertyBlock.SetColor(OutlineColorId, TraitOutline());
        propertyBlock.SetFloat(FlashId, flash);
        sprite.SetPropertyBlock(propertyBlock);
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
        if (regenDelay <= 0f) return outline;

        float charge = RegenCharge();
        float speed = MissingDurability() ? Mathf.Lerp(2.4f, 9f, charge) : 1.6f;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * Mathf.PI * 2f);
        float amount = MissingDurability() ? Mathf.Lerp(0.25f, 1f, charge) * wave : 0.3f * wave;
        return Color.Lerp(outline, RegenHot, Mathf.Max(amount, healPulse));
    }

    bool RejectsArmor(Vector2 worldPoint)
    {
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

    void PlayPlateDeflect()
    {
        plateFlash = 0.16f;
    }

    void TickPlate()
    {
        if (plateMarks == null || plateFlash < 0f) return;

        plateFlash -= Time.deltaTime;
        float white = plateFlash <= 0f ? 0f : Mathf.Clamp01(plateFlash / 0.1f);
        for (int i = 0; i < plateMarks.Length; i++)
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
        if (regenTimer < regenDelay) return;

        regenTimer = 0f;
        RestoreStep();
    }

    float RegenCharge()
    {
        if (regenDelay <= 0f || !MissingDurability()) return 0f;
        return Mathf.Clamp01(regenTimer / regenDelay);
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
