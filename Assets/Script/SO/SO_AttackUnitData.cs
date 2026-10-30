using UnityEngine;

/// <summary>
/// 攻撃ユニット。
/// attackRate は攻撃時の発生確率。
/// baseDamage はその攻撃が与えるダメージ。
/// generateRate は継続間隔。サークルのダメージ間隔と、レーザーが太くなる時間。
/// generateCount は 1 回の発生数。
/// size は範囲や太さ。speed は弾速。duration は持続時間や表示時間。
/// </summary>
[System.Serializable]
public class AttackUnitData
{
    public int index;
    public string attackName;
    public AttackPatternBase pf;
    public float attackRate;
    public float baseDamage;
    public float generateRate;
    public int generateCount = 1;
    public float size;
    public float speed;
    public float duration;
}

[CreateAssetMenu(menuName = "SO/AttackUnitData")]
public class SO_AttackUnitData : ScriptableObject
{
    public AttackUnitData[] attackUnitDatas;

    public AttackUnitData Find(int index)
    {
        if (attackUnitDatas == null) return null;

        for (int i = 0; i < attackUnitDatas.Length; i++)
        {
            var data = attackUnitDatas[i];
            if (data != null && data.index == index)
                return data;
        }
        return null;
    }

    /// <summary>
    /// CSV の結果を反映する。pf が空の行は、同じ index の既存プレハブを残す。
    /// </summary>
    public void ApplyLoaded(AttackUnitData[] loaded)
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
        attackUnitDatas = loaded;
    }
}
