using UnityEngine;

/// <summary>
/// ブロック崩しで使うプレハブ。パスではなく参照で持つ。
/// </summary>
[CreateAssetMenu(menuName = "SO/BlockSession", fileName = "SO_BlockSession")]
public class SO_BlockSession : ScriptableObject
{
    [Header("ボール")]
    public BlockBall normalBall;
    public BlockBall bombBall;
    public BlockBall laserBall;
    public BlockBall laserRowBall;
    public BlockBall heavyBall;
    public BlockBall orbitBall;
    public BlockBall crossBall;
    public BlockBall poisonBall;

    [Space(10)]
    [Header("フィールド")]
    public GameObject leftWall;
    public GameObject rightWall;
    public GameObject deadline;
    public GameObject laserBeam;
    public GameObject laserBeamCross;
    public GameObject poisonPool;

    [Space(10)]
    [Header("UI")]
    public UI_BlockCursorInfo cursorInfo;
    public RectTransform cursorBase;
    public BlockAimGuide aimGuide;

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
    [Tooltip("開始時の、次のブロック群までの秒数。")]
    public float baseInterval = 8f;
    [Tooltip("1秒ごとに短くなる生成間隔。")]
    public float intervalShrinkPerSecond = 0.045f;
    [Tooltip("生成間隔の下限（秒）。")]
    public float minInterval = 4.2f;

    [Space(10)]
    [Header("HP")]
    [Tooltip("1マスの開始HP。ステージの blockHpRate をさらに掛ける。")]
    public int baseHp = 2;
    [Tooltip("1秒ごとに足すHP。")]
    public float hpPerSecond = 0.1f;
    [Tooltip("1マス増えるごとに足すHP倍率。各ブロックプレハブの hpScale もさらに掛ける。")]
    public float sizeHpScale = 1f;

    [Space(10)]
    [Header("ショット")]
    [Tooltip("通常ボールの数。")]
    public int ballCount = 3;
    [Tooltip("ボールの速さ。ヘビーボールは heavySpeedScale をさらに掛ける。")]
    public float ballSpeed = 13f;
    [Tooltip("連射の間隔（秒）。")]
    public float shotInterval = 0.2f;
    [Tooltip("ボール1発、爆発、レーザーのダメージ。")]
    public int ballDamage = 1;
    [Tooltip("これより短いドラッグは発射しない。ワールド単位。")]
    public float minPull = 0.28f;
    [Tooltip("ヘビーボールの速度倍率。")]
    public float heavySpeedScale = 0.5f;
    [Tooltip("爆発の半径。ブロック1マスを 1 とする。")]
    public float bombRadiusInCells = 2.5f;
    [Tooltip("沼が残る秒数。")]
    public float poisonDuration = 2.5f;
    [Tooltip("沼が攻撃する半径。ブロック1マスを 1 とする。")]
    public float poisonRadiusInCells = 1.6f;
    [Tooltip("沼が周囲を攻撃する間隔（秒）。")]
    public float poisonInterval = 0.4f;
    [Tooltip("同時に置ける沼の数。スキルツリーで増やす前の初期値。")]
    public int poisonMax = 2;

    [Space(10)]
    [Header("盤面")]
    [Tooltip("ブロック同士の隙間。ワールド単位。")]
    public float blockGap = 0.1f;
    [Tooltip("10列のときのブロックサイズ。カメラ幅に対する比率。0.5 なら幅の 20分の1。")]
    public float blockSizeScale = 0.5f;
    [Tooltip("ステージの列数が 0 以下のときの列数。")]
    public int defaultColumns = 10;
    [Tooltip("画面上端にある、見えない天井の厚さ。")]
    public float wallThickness = 0.3f;
    [Tooltip("カメラの外まで壁を塗る余白。画面シェイクで隙間が出ない量。")]
    public float wallOverscan = 2f;

    [Space(10)]
    [Header("照準")]
    [Tooltip("レーザーを表示している時間（秒）。")]
    public float beamLife = 0.32f;
    [Tooltip("レーザーが消え始めるまでの待ち時間（秒）。表示時間に含まれる。")]
    public float beamHold = 0.18f;
    [Tooltip("照準線が壁やブロックで跳ねる最大回数。")]
    public int aimBounces = 12;
    [Tooltip("跳ねた直後に壁から離す距離。めり込み防止。")]
    public float aimSkin = 0.02f;

    [Space(10)]
    [Header("回収")]
    [Tooltip("カーソル中心から資源を拾う半径。ワールド単位。スキルの拾得範囲が 0.5 なら 1.5 倍。")]
    public float pickupRange = 0.4f;
    [Tooltip("射撃カーソルの追加回収で、当たり判定より狭くしない半径。スキルの拾得範囲があるときだけ使う。")]
    public float cursorPickupRadius = 0.5f;
    [Tooltip("拾う直前に、カーソルの反対側へ跳ねる距離。")]
    public float collectKickDistance = 0.45f;
    [Tooltip("反対側へ跳ねる時間（秒）。")]
    public float collectKickDuration = 0.1f;
    [Tooltip("跳ねたあと、カーソルへ吸い込む時間（秒）。")]
    public float collectPullDuration = 0.18f;

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
            case BlockBallKind.Cross: return crossBall;
            case BlockBallKind.Poison: return poisonBall;
            default: return null;
        }
    }
}
