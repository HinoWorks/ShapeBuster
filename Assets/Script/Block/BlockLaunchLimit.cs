using UnityEngine;

/// <summary>
/// 発射できる範囲の上端を点線で示す。見た目は自身のスプライト。長さと間隔はここの数値。
/// </summary>
public class BlockLaunchLimit : MonoBehaviour
{
    [SerializeField] float dashLength = 0.3f;
    [SerializeField] float dashGap = 0.16f;

    public void Place(float left, float right, float y)
    {
        var source = GetComponent<SpriteRenderer>();
        if (source == null) return;

        float width = right - left;
        float thickness = WorldHeight(source);
        if (thickness < 0.0001f)
            thickness = 0.045f;

        transform.position = new Vector3((left + right) * 0.5f, y, transform.position.z);
        transform.localScale = Vector3.one;
        source.enabled = false;
        if (width < 0.02f) return;

        float mark = Mathf.Max(0.02f, dashLength);
        float step = mark + Mathf.Max(0f, dashGap);
        float localLeft = -width * 0.5f;
        float cursor = 0f;
        while (cursor < width - 0.001f)
        {
            float length = Mathf.Min(mark, width - cursor);
            if (length < 0.02f) break;

            var bar = Spawn(source);
            bar.transform.localPosition = new Vector3(localLeft + cursor + length * 0.5f, 0f, 0f);
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = new Vector3(1f, thickness, 1f);
            bar.drawMode = SpriteDrawMode.Tiled;
            bar.size = new Vector2(length, 1f);
            cursor += step;
        }
    }

    static float WorldHeight(SpriteRenderer sprite)
    {
        float scale = Mathf.Abs(sprite.transform.localScale.y);
        if (sprite.drawMode == SpriteDrawMode.Simple)
        {
            float native = sprite.sprite != null ? sprite.sprite.bounds.size.y : 1f;
            return scale * Mathf.Max(0.0001f, native);
        }
        return scale * Mathf.Max(0.0001f, sprite.size.y);
    }

    SpriteRenderer Spawn(SpriteRenderer source)
    {
        var host = new GameObject("Dash");
        host.transform.SetParent(transform, false);
        var bar = host.AddComponent<SpriteRenderer>();
        bar.sprite = source.sprite;
        bar.color = source.color;
        bar.sharedMaterial = source.sharedMaterial;
        bar.sortingLayerID = source.sortingLayerID;
        bar.sortingOrder = -1;
        bar.flipX = source.flipX;
        bar.flipY = source.flipY;
        return bar;
    }
}
