using UnityEngine;

/// <summary>
/// 発射、照準、資源の回収、カーソルUI。GSS では管理しない。
/// </summary>
[CreateAssetMenu(menuName = "SO/Player", fileName = "SO_Player")]
public class SO_Player : ScriptableObject
{
    static SO_Player current;

    public static SO_Player Current
    {
        get
        {
            if (current != null) return current;
            current = Resources.Load<SO_Player>("SO/SO_Player");
            if (current != null) return current;

            Debug.LogWarning("SO_Player がありません。初期値で進めます。");
            current = CreateInstance<SO_Player>();
            return current;
        }
    }

    [Header("UI")]
    public UI_BlockCursorInfo cursorInfo;
    public RectTransform cursorBase;
    public BlockAimGuide aimGuide;

    [Space(10)]
    [Header("ショット")]
    [Tooltip("連射の間隔（秒）。")]
    public float shotInterval = 0.2f;
    [Tooltip("これより短いドラッグは発射しない。ワールド単位。")]
    public float minPull = 0.28f;
    [Tooltip("画面上端から、発射の基点にできない距離。ワールド単位。この線より上は基点にならない。0 なら制限しない。")]
    public float launchTopInset = 4.5f;

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
    public float pickupRange = 0.5f;
    [Tooltip("射撃カーソルの追加回収で、当たり判定より狭くしない半径。スキルの拾得範囲があるときだけ使う。")]
    public float cursorPickupRadius = 0.5f;
    [Tooltip("拾う直前に、カーソルの反対側へ跳ねる距離。")]
    public float collectKickDistance = 0.45f;
    [Tooltip("反対側へ跳ねる時間（秒）。")]
    public float collectKickDuration = 0.1f;
    [Tooltip("跳ねたあと、カーソルへ吸い込む時間（秒）。")]
    public float collectPullDuration = 0.18f;

    [Space(10)]
    [Header("砲台")]
    [Tooltip("オンのあいだは、スキル未取得でも左右の砲台を出す。")]
    public bool debugTurrets = true;
    public BlockTurret turret;
    public BlockBall turretBall;

    [Space(10)]
    [Header("横ビーム")]
    [Tooltip("オンのあいだは、スキル未取得でも横ビームを撃つ。")]
    public bool debugRowBeam = true;
    [Tooltip("発射体が一番下のブロックへ寄る速さ。ワールド単位/秒。間隔と威力は Ball シート。")]
    public float rowBeamMoveSpeed = 8f;
    public BlockRowEmitter rowEmitter;
    [Tooltip("色はプレハブの SpriteRenderer で変える。")]
    public GameObject rowBeam;
}
