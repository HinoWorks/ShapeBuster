using UnityEngine;

/// <summary>
/// ブロックのレベル色。配列の先頭がレベル1。
/// レベル0は壊れた状態で、色は持たない。
/// </summary>
[CreateAssetMenu(menuName = "SO/BlockLevel", fileName = "SO_BlockLevel")]
public class SO_BlockLevel : ScriptableObject
{
    static readonly Color[] Fallback =
    {
        new Color(0.45f, 0.62f, 0.38f, 1f),
        new Color(0.25f, 0.55f, 0.82f, 1f),
        new Color(0.95f, 0.78f, 0.28f, 1f),
        new Color(0.93f, 0.48f, 0.18f, 1f),
        new Color(0.82f, 0.28f, 0.32f, 1f),
        new Color(0.62f, 0.36f, 0.78f, 1f),
    };

    [Tooltip("要素0がレベル1。後ろの要素ほど上のレベル。")]
    public Color[] colors;

    public static int MaxCount
    {
        get
        {
            var table = SOLoader.BlockLevel;
            if (table != null && table.colors != null && table.colors.Length > 0)
                return table.colors.Length;
            return Fallback.Length;
        }
    }

    /// <summary>レベルは1始まり。0以下は透過。</summary>
    public static Color ColorFor(int level)
    {
        if (level <= 0)
            return new Color(0f, 0f, 0f, 0f);

        var table = SOLoader.BlockLevel;
        var palette = table != null && table.colors != null && table.colors.Length > 0
            ? table.colors
            : Fallback;
        var color = palette[Mathf.Min(level, palette.Length) - 1];
        color.a = 1f;
        return color;
    }
}
