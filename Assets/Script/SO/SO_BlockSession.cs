using UnityEngine;

/// <summary>
/// ブロック崩しで使うプレハブ。パスではなく参照で持つ。
/// </summary>
[CreateAssetMenu(menuName = "SO/BlockSession", fileName = "SO_BlockSession")]
public class SO_BlockSession : ScriptableObject
{
    public BlockBall normalBall;
    public BlockBall bombBall;
    public BlockBall laserBall;
    public BlockBall laserRowBall;
    public BlockBall heavyBall;
    public BlockBall orbitBall;

    public GameObject leftWall;
    public GameObject rightWall;
    public GameObject deadline;
    public GameObject laserBeam;

    public UI_BlockCursorInfo cursorInfo;
    public RectTransform cursorBase;
    public BlockAimGuide aimGuide;

    public BlockBall Ball(BlockBallKind kind)
    {
        switch (kind)
        {
            case BlockBallKind.Normal: return normalBall;
            case BlockBallKind.Bomb: return bombBall;
            case BlockBallKind.Laser: return laserBall;
            case BlockBallKind.LaserRow: return laserRowBall;
            case BlockBallKind.Heavy: return heavyBall;
            case BlockBallKind.Orbit: return orbitBall;
            default: return null;
        }
    }
}
