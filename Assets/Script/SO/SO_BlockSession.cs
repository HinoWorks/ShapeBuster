using UnityEngine;

/// <summary>
/// ブロック崩しで使うプレハブ。パスではなく参照で持つ。
/// </summary>
[CreateAssetMenu(menuName = "SO/BlockSession", fileName = "SO_BlockSession")]
public class SO_BlockSession : ScriptableObject
{
    [Header("フィールド")]
    public GameObject leftWall;
    public GameObject rightWall;
    public GameObject deadline;
    public GameObject launchLimit;
    public GameObject laserBeam;
    public GameObject laserBeamCross;
    public GameObject poisonPool;

    [Space(10)]
    [Header("ブロック")]
    public BlockPiece square;
    public BlockPiece wide;
    public BlockPiece tall;
    public BlockPiece solid;
    public BlockPiece armor;
    public BlockPiece regen;
    public BlockPiece boss;

    [Space(10)]
    [Header("配置")]
    [Tooltip("開始時にブロックで埋める割合。0.7 なら 70%。")]
    public float baseFill = 0.7f;
    [Tooltip("ボス出現後の配置率に掛ける倍率。出現自体は続く。")]
    public float bossFillScale = 0.7f;
    [Tooltip("時間で増える配置率の上限。")]
    public float maxFill = 0.85f;
    [Tooltip("1秒ごとに足す配置率。")]
    public float fillPerSecond = 0.002f;

    [Space(10)]
    [Header("下降")]
    [Tooltip("開始時の下降速度。ワールド単位/秒。ステージの blockSpeedRate をさらに掛ける。")]
    public float baseSpeed = 0.1f;
    [Tooltip("1秒ごとに足す下降速度。")]
    public float speedPerSecond = 0.005f;

    [Space(10)]
    [Header("HP")]
    [Tooltip("1マスの開始HP。ステージの blockHpRate をさらに掛ける。")]
    public int baseHp = 2;
    [Tooltip("1秒ごとに足すHP。")]
    public float hpPerSecond = 0.1f;
    [Tooltip("1マス増えるごとに足すHP倍率。各ブロックプレハブの hpScale もさらに掛ける。")]
    public float sizeHpScale = 1f;

    [Space(10)]
    [Header("盤面")]
    [Tooltip("ブロック同士の隙間。ワールド単位。")]
    public float blockGap = 0.1f;
    [Tooltip("端のブロックの外縁と、壁の内側面との隙間。ワールド単位。")]
    public float wallSideGap = 0.15f;
    [Tooltip("10列のときのブロックサイズ。カメラ幅に対する比率。0.5 なら幅の 20分の1。")]
    public float blockSizeScale = 0.5f;
    [Tooltip("ステージの列数が 0 以下のときの列数。")]
    public int defaultColumns = 10;
    [Tooltip("画面上端にある、見えない天井の厚さ。")]
    public float wallThickness = 0.3f;
    [Tooltip("カメラの外まで壁を塗る余白。画面シェイクで隙間が出ない量。")]
    public float wallOverscan = 2f;
}
