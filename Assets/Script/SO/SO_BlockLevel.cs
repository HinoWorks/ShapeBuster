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
        // ゾンビの肌色。青白い灰緑から、腐るほど暗く濁る。
        new Color(0.64f, 0.72f, 0.60f, 1f),
        new Color(0.47f, 0.64f, 0.36f, 1f),
        new Color(0.64f, 0.60f, 0.30f, 1f),
        new Color(0.33f, 0.54f, 0.56f, 1f),
        new Color(0.52f, 0.38f, 0.58f, 1f),
        new Color(0.60f, 0.28f, 0.25f, 1f),
        new Color(0.22f, 0.40f, 0.28f, 1f),
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
