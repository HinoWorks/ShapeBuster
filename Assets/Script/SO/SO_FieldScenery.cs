using System;
using UnityEngine;

/// <summary>景色の見せ方。SO_FieldScenery の activeStyle と同じ見せ方の行だけを使う。</summary>
public enum FieldSceneryStyle
{
    /// <summary>横から見た町並み。帯の下側に 1 枚。</summary>
    Street = 0,
    /// <summary>上から見下ろした町。帯の上から下まで、縦にくり返して敷く（Ball x Pit のように、横に木や家が並ぶ）。</summary>
    TopDown = 1,
}

/// <summary>
/// フィールドの左右の帯に出す景色の 1 か所（住宅地・公園など）。fromStage〜toStage の段で出す。
/// 絵はワクチンを撒く前（before）と後（after）で分ける。after が空なら before を使う。
/// 右の絵が空なら、左の絵を左右反転して使う。
/// </summary>
[Serializable]
public class FieldSceneryEntry
{
    [Tooltip("見分けるための名前（画面には出さない）。")]
    public string memo;
    [Tooltip("見せ方。SO_FieldScenery の activeStyle と同じ行だけを使う。")]
    public FieldSceneryStyle style;
    [Tooltip("この景色を出す最初の段（stageIndex、0 から）。")]
    public int fromStage;
    [Tooltip("この景色を出す最後の段（stageIndex）。")]
    public int toStage;
    [Tooltip("縦にくり返して、帯の上から下まで敷く。オフなら、絵の下の端を画面の下の端にそろえて 1 枚だけ。")]
    public bool tileVertical;
    [Tooltip("絵とフィールドの縁とのすき間（ワールド）。")]
    public float edgeGap = 0.35f;
    [Tooltip("左の帯の絵（右の端がフィールド側）。ワクチンを撒く前。")]
    public Texture2D leftBefore;
    [Tooltip("左の帯の絵。撒いたあと。空なら撒く前の絵。")]
    public Texture2D leftAfter;
    [Tooltip("右の帯の絵（左の端がフィールド側）。撒く前。空なら左の絵を反転して使う。")]
    public Texture2D rightBefore;
    [Tooltip("右の帯の絵。撒いたあと。空なら撒く前の絵。")]
    public Texture2D rightAfter;

    public bool Covers(int stageIndex) => stageIndex >= fromStage && stageIndex <= toStage;
}

/// <summary>
/// フィールドの左右の帯の景色。Resources/SO/SO_FieldScenery から読む。
/// 見せ方（activeStyle）を変えると、同じ段でも別の絵の組に切り替わる。どちらの組も残しておける。
/// 絵は IconGen/field_scenery.py で作る（1 ワールド単位 = pixelsPerUnit ドット）。
/// 貼り方は BlockSession.SetScenery、重ね方は FieldSide シェーダー。
/// </summary>
[CreateAssetMenu(menuName = "SO/FieldScenery")]
public class SO_FieldScenery : ScriptableObject
{
    [Tooltip("今使う見せ方。")]
    public FieldSceneryStyle activeStyle = FieldSceneryStyle.Street;
    [Tooltip("絵の 1 ワールド単位あたりのドット数。ブロックの絵のドットに近い 12。")]
    public float pixelsPerUnit = 12f;
    public FieldSceneryEntry[] entries = Array.Empty<FieldSceneryEntry>();

    /// <summary>その段の、今の見せ方の景色。なければ null。</summary>
    public FieldSceneryEntry Find(int stageIndex)
    {
        if (entries == null) return null;
        foreach (var entry in entries)
        {
            if (entry != null && entry.style == activeStyle && entry.Covers(stageIndex)) return entry;
        }
        return null;
    }
}
