using UnityEngine;

/// <summary>
/// プレイ平面上のポインタ座標。毎フレーム PointerInput が書き、追従側が読む。
/// </summary>
public static class PointerWorld
{
    public static Vector3 Position { get; private set; }
    public static bool IsInsidePlayfield { get; private set; }

    public static void Set(Vector3 position, bool isInsidePlayfield)
    {
        Position = position;
        IsInsidePlayfield = isInsidePlayfield;
    }
}
