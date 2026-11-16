using System;
using UnityEngine;

/// <summary>
/// ボール1種。index は BlockBallKind。GSS の Ball シートで管理する。
/// タレットは 10、オートビームは 11。pf は Assets からのプレハブパス。空なら前回の参照を残す。
/// </summary>
[Serializable]
public class BallData
{
    public int index;
    public string ballName;
    public BlockBall pf;
    /// <summary>ノーマル攻撃力に掛ける素の比率。特殊弾はここに BallRatio を足す。空や0はノーマル1、それ以外0.5。</summary>
    public float baseDamageRate = 1f;
    /// <summary>当たって壊したときの追加効果の発生率。0.1 で 10%。スキルの BallProc を足す。出さない種類は 0。</summary>
    public float baseProcRate;
    /// <summary>見た目の倍率。1 でプレハブの大きさのまま。</summary>
    public float baseSize = 1f;
    /// <summary>飛ぶ速さ。スキルの球速をさらに掛ける。</summary>
    public float baseSpeed = 13f;
    /// <summary>爆風や沼の半径。ブロック1マスを 1 とする。使わない種類は 0。</summary>
    public float areaRadius;
    /// <summary>秒数。ポイズンは沼が残る時間。タレットとオートビームは攻撃間隔。</summary>
    public float effectDuration;
    /// <summary>沼が周囲を攻撃する間隔（秒）。使わない種類は 0。</summary>
    public float effectInterval;
    /// <summary>同時に置ける沼の数。使わない種類は 0。</summary>
    public int effectMax;
}

/// <summary>ボール一覧。GSS の Ball シートを読み込む。</summary>
[CreateAssetMenu(menuName = "SO/BallData", fileName = "SO_BallData")]
public class SO_BallData : ScriptableObject
{
    public BallData[] ballDatas;

    [Space(10)]
    [Header("砲台")]
    [Tooltip("オンのあいだは、スキル未取得でも左右の砲台を出す。")]
    public bool debugTurrets = true;
    public BlockTurret turret;

    [Space(10)]
    [Header("横ビーム")]
    [Tooltip("オンのあいだは、スキル未取得でも横ビームを撃つ。")]
    public bool debugRowBeam = true;
    [Tooltip("発射体が一番下のブロックへ寄る速さ。ワールド単位/秒。間隔と威力は Ball シート。")]
    public float rowBeamMoveSpeed = 8f;
    public BlockRowEmitter rowEmitter;
    [Tooltip("色はプレハブの SpriteRenderer で変える。")]
    public GameObject rowBeam;

    public BallData Find(BlockBallKind kind)
    {
        return Find((int)kind);
    }

    public BallData Find(int index)
    {
        if (ballDatas == null) return null;
        for (int i = 0; i < ballDatas.Length; i++)
        {
            var data = ballDatas[i];
            if (data != null && data.index == index)
                return data;
        }
        return null;
    }

    /// <summary>シートで pf が空の行は、同じ index の既存プレハブを残す。</summary>
    public void ApplyImported(BallData[] loaded)
    {
        if (loaded == null) return;
        for (int i = 0; i < loaded.Length; i++)
        {
            var row = loaded[i];
            if (row == null || row.pf != null) continue;
            var previous = Find(row.index);
            if (previous != null)
                row.pf = previous.pf;
        }
        ballDatas = loaded;
    }
}
