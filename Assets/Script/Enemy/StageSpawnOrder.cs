using UnityEngine;

/// <summary>
/// 指定生成の並び。Random は範囲内のばらまき、Circle は円周、Line は直線。
/// </summary>
public enum SpawnShape
{
    Random,
    Circle,
    Line,
}

/// <summary>
/// 指定生成の高さ。X は出現範囲内でランダム。
/// 0 画面上部の外（通常の出現位置）、1 画面上部、2 画面中央。
/// </summary>
public enum SpawnBand
{
    OutsideTop = 0,
    ScreenTop = 1,
    ScreenCenter = 2,
}

/// <summary>
/// 出現率とは別に、時刻になったら指定の敵を出す 1 行。
/// シート名 StageSpawnOrder。shape は Random / Circle / Line、または 0 / 1 / 2。
/// spawnBand は 0 / 1 / 2。size は Circle の半径、Line の長さ。
/// angle は度。0 が右、90 が上。Circle と Line は帯の中央の高さに、X をランダムにして置く。
/// </summary>
[System.Serializable]
public class StageSpawnOrder
{
    public int stageIndex;
    public float spawnSecond;
    public int enemyIndex;
    public int count;
    public SpawnBand spawnBand;
    public SpawnShape shape;
    public float size;
    public float angle;

    /// <summary>xRange は (minX, maxX)、yRange は (minY, maxY)。</summary>
    public Vector3[] BuildPositions(Vector2 xRange, Vector2 yRange)
    {
        if (count <= 0) return System.Array.Empty<Vector3>();

        var positions = new Vector3[count];
        switch (shape)
        {
            case SpawnShape.Circle:
                FillCircle(positions, FormationAnchor(xRange, yRange));
                break;
            case SpawnShape.Line:
                FillLine(positions, FormationAnchor(xRange, yRange));
                break;
            default:
                FillRandom(positions, xRange, yRange);
                break;
        }
        return positions;
    }

    static Vector3 FormationAnchor(Vector2 xRange, Vector2 yRange)
    {
        return new Vector3(
            Random.Range(xRange.x, xRange.y),
            (yRange.x + yRange.y) * 0.5f,
            0f);
    }

    void FillCircle(Vector3[] positions, Vector3 center)
    {
        int n = positions.Length;
        float start = angle * Mathf.Deg2Rad;
        for (int i = 0; i < n; i++)
        {
            float t = start + (Mathf.PI * 2f * i / n);
            positions[i] = center + new Vector3(Mathf.Cos(t) * size, Mathf.Sin(t) * size, 0f);
        }
    }

    void FillLine(Vector3[] positions, Vector3 center)
    {
        int n = positions.Length;
        if (n == 1)
        {
            positions[0] = center;
            return;
        }

        var dir = (Vector3)(Quaternion.Euler(0f, 0f, angle) * Vector2.right);
        var origin = center - dir * (size * 0.5f);
        float step = size / (n - 1);
        for (int i = 0; i < n; i++)
            positions[i] = origin + dir * (step * i);
    }

    void FillRandom(Vector3[] positions, Vector2 xRange, Vector2 yRange)
    {
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = new Vector3(
                Random.Range(xRange.x, xRange.y),
                Random.Range(yRange.x, yRange.y),
                0f);
        }
    }
}
