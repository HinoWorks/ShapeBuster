using UnityEngine;

/// <summary>
/// ブロック1個。プレハブのマス数、色、HP倍率、壊れない設定を使う。
/// 見た目は BlockLevelFill。現在レベルの色を下から塗り、空きは一つ下の色。レベル1の空きは透過。
/// 輪郭の太さはスケールが変わっても同じにする。
/// 被弾で白く光り、一瞬大きくなる。破壊時も同じ演出を出し、その場に破壊エフェクトを出す。
/// </summary>
public class BlockPiece : MonoBehaviour
{
    const string LevelShader = "ShapeBuster/BlockLevelFill";
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

    [Tooltip("横のマス数")]
    [SerializeField] int cellsX = 1;
    [Tooltip("縦のマス数")]
    [SerializeField] int cellsY = 1;
    [Tooltip("壊せない。デッドラインに触れても終了しない")]
    [SerializeField] bool solid;
    [Tooltip("出現しやすさ")]
    [SerializeField] float weight = 1f;
    [Tooltip("マス数による倍率に、さらに掛けるHP倍率")]
    [SerializeField] float hpScale = 1f;
    [SerializeField] SpriteRenderer sprite;

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
    Color fillColor = Color.white;
    Color underColor = new Color(0f, 0f, 0f, 0f);
    Color pushedFill = new Color(-1f, -1f, -1f, -1f);
    Color pushedUnder = new Color(-1f, -1f, -1f, -1f);
    Color baseOutline = Color.white;
    float flash;
    float reactTime = -1f;
    bool breaking;
    bool outlineReady;

    public int CellsX => Mathf.Max(1, cellsX);
    public int CellsY => Mathf.Max(1, cellsY);
    public float Weight => Mathf.Max(0f, weight);
    public float HpScale => Mathf.Max(0f, hpScale);
    public bool IsAlive { get; private set; }
    public bool IsSolid => solid;
    public float Bottom => transform.position.y - half;

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
        half = worldSize.y * 0.5f;
        restScale = transform.localScale;
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

        if (!solid && (sprite.sharedMaterial == null || sprite.sharedMaterial.shader == null
            || sprite.sharedMaterial.shader.name != LevelShader))
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
    }

    public void Hit(int damage)
    {
        if (!IsAlive || solid) return;

        int applied = Mathf.Max(1, damage);
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

        GameEvent.InGame.PublishDamageDealt(applied, -1);
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

        int reward = StageEnemyScale.ScaleResourceCount(
            rewardCells,
            1f + Mathf.Max(0f, SkillTreeBonus.CursorResourceUpRate));
        if (reward > 0)
            GameEvent.UI.PublishResourceMod_Ingame(ResourceType.AA, reward);

        session.NotifyBroken(this);
        if (EffectManager.Inst != null)
            EffectManager.Inst.Rent(EffectType.BlockBreak, transform.position);
        if (CameraManager.Inst != null)
            CameraManager.Inst.Shake(CameraShakeLevel.Small);
        PlayReact(true);
    }

    void PlayReact(bool willBreak)
    {
        if (willBreak)
            breaking = true;

        reactTime = 0f;
        flash = 1f;
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

    void PushFill()
    {
        if (sprite == null) return;
        if (sprite.sharedMaterial == null || sprite.sharedMaterial.shader == null) return;
        if (sprite.sharedMaterial.shader.name != LevelShader) return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        int spanX = Mathf.Max(1, Application.isPlaying ? placedX : CellsX);
        int spanY = Mathf.Max(1, Application.isPlaying ? placedY : CellsY);
        if (!outlineReady)
            CacheOutline();

        float width = Mathf.Max(0.0001f, sprite.sharedMaterial.GetFloat(OutlineWidthId));
        if (propertyBlock != null && width == pushedWidth && spanX == pushedX && spanY == pushedY
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
        propertyBlock.SetColor(OutlineColorId, Color.Lerp(baseOutline, Color.white, flash));
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
