using DG.Tweening;
using UnityEngine;

/// <summary>
/// スプライトの輪郭は残し、内側だけを下から HP 割合で塗る。
/// 塗りの模様はマテリアルの地色とドット色。割合は頂点カラーの A に入れ、敵ごとに持つ。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHpFill : MonoBehaviour
{
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    static readonly int FlashId = Shader.PropertyToID("_Flash");

    [SerializeField] Material fillMaterial;
    [SerializeField] Color outlineColor = Color.white;
    [SerializeField, Range(0.005f, 0.2f)] float outlineWidth = 0.045f;

    SpriteRenderer spriteRenderer;
    MaterialPropertyBlock block;
    Color fillColor = Color.white;
    float ratio = 1f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        fillColor = spriteRenderer.color;
        fillColor.a = 1f;
        block = new MaterialPropertyBlock();
        ApplyMaterial();
        ApplyOutline();
    }

    void OnEnable()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        ratio = 1f;
        ApplyOutline();
        ApplyFill();
    }

    public void SetRatio(float value)
    {
        ratio = Mathf.Clamp01(value);
        ApplyFill();
    }

    /// <summary>塗りと輪郭を白くして、すぐ元の色へ戻す。アルファの塗り割合は維持する。</summary>
    public void PlayDamageFlash()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        spriteRenderer.DOKill();
        DOTween.Kill(this);

        var flashed = Color.white;
        flashed.a = ratio;
        spriteRenderer.color = flashed;
        var restore = fillColor;
        restore.a = ratio;
        spriteRenderer.DOColor(restore, 0.1f);

        var outline = outlineColor;
        ApplyOutlineColor(Color.white);
        ApplyFlash(1f);
        DOVirtual.Float(0f, 1f, 0.1f, t =>
            ApplyOutlineColor(Color.Lerp(Color.white, outline, t))).SetTarget(this);
        DOVirtual.Float(1f, 0f, 0.1f, ApplyFlash).SetTarget(this);
    }

    public void RestoreVisual()
    {
        if (spriteRenderer != null)
            spriteRenderer.DOKill();
        DOTween.Kill(this);
        ApplyFill();
        ApplyOutline();
        ApplyFlash(0f);
    }

    void OnDisable()
    {
        RestoreVisual();
    }

    void OnValidate()
    {
        outlineWidth = Mathf.Clamp(outlineWidth, 0.005f, 0.2f);
    }

    void ApplyMaterial()
    {
        if (fillMaterial != null && spriteRenderer.sharedMaterial != fillMaterial)
            spriteRenderer.sharedMaterial = fillMaterial;
    }

    void ApplyOutline()
    {
        ApplyOutlineColor(outlineColor);
    }

    void ApplyOutlineColor(Color color)
    {
        if (spriteRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        spriteRenderer.GetPropertyBlock(block);
        block.SetColor(OutlineColorId, color);
        block.SetFloat(OutlineWidthId, outlineWidth);
        spriteRenderer.SetPropertyBlock(block);
    }

    void ApplyFlash(float flash)
    {
        if (spriteRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        spriteRenderer.GetPropertyBlock(block);
        block.SetFloat(FlashId, flash);
        spriteRenderer.SetPropertyBlock(block);
    }

    void ApplyFill()
    {
        if (spriteRenderer == null) return;

        var color = fillColor;
        color.a = Mathf.Clamp01(ratio);
        spriteRenderer.color = color;
    }
}
