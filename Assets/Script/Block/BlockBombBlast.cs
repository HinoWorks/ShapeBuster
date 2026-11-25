using UnityEngine;

/// <summary>
/// ボムボールの爆発の見た目（pf_BombBlast）。攻撃範囲の円の大きさに合わせて置き、
/// シェーダー（ShapeBuster/SpriteBombBlast）の _Progress を 0→1 に進めて、終わったら消える。
/// 絵はシェーダーが円の中にだけ描くので、スプライトは正方形（1 ワールド単位）を使う。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BlockBombBlast : MonoBehaviour
{
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    static readonly int SeedId = Shader.PropertyToID("_Seed");

    [Tooltip("爆発が消えるまでの秒数")]
    [SerializeField] float life = 0.5f;

    SpriteRenderer sprite;
    MaterialPropertyBlock block;
    float age;
    float seed;

    /// <summary>爆心と攻撃範囲の半径（ワールド）を決めて再生する。</summary>
    public void Play(Vector2 center, float radius)
    {
        sprite = GetComponent<SpriteRenderer>();
        if (sprite.sprite == null)
            sprite.sprite = BlockSession.SquareSprite;
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        // 正方形のスプライトは 1 ワールド単位なので、直径をそのまま大きさにする。
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        block ??= new MaterialPropertyBlock();
        seed = Random.Range(0f, 100f);
        age = 0f;
        Apply(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(0.05f, life));
        Apply(t);
        if (t >= 1f)
            Destroy(gameObject);
    }

    void Apply(float progress)
    {
        if (sprite == null) return;
        sprite.GetPropertyBlock(block);
        block.SetFloat(ProgressId, progress);
        block.SetFloat(SeedId, seed);
        sprite.SetPropertyBlock(block);
    }
}
