using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ゾンビ図鑑の研究を進める条件で数えるもの。</summary>
public enum BestiaryCount
{
    /// <summary>倒した数。</summary>
    Kill = 0,
    /// <summary>ボールを当てた回数（倒せない石）。爆発・レーザー・毒は数えない。</summary>
    Hit = 1,
}

/// <summary>ゾンビ図鑑の研究で伸びる効果。値はアセットに保存されるので、途中に挟まず末尾に足す。</summary>
public enum BestiaryEffect
{
    None = 0,
    /// <summary>普通のゾンビへのダメージ（割合）。</summary>
    NormalDamage = 1,
    /// <summary>装甲の殻に当たったとき、本体に通るダメージ（割合）。</summary>
    ArmorPierce = 2,
    /// <summary>再生ゾンビの回復が始まるまでの時間（割合）。</summary>
    RegenDelay = 3,
    /// <summary>膨張ゾンビの爆発の範囲（割合）。</summary>
    BloatRadius = 4,
    /// <summary>分裂ゾンビの分かれた子の HP（割合で減らす）。</summary>
    SplitHp = 5,
    /// <summary>幽霊ゾンビが実体でいる時間（割合）。</summary>
    GhostSolid = 6,
    /// <summary>ガラスへのダメージ（割合）。</summary>
    GlassDamage = 7,
    /// <summary>宝箱の出やすさ（割合）。</summary>
    ChestWeight = 8,
    /// <summary>全ボールの攻撃力（割合）。</summary>
    AllDamage = 9,
}

/// <summary>
/// ゾンビ図鑑の 1 項目。スプレッドシート Bestiary の列名とフィールド名をそろえる。
/// 配列は 1|4|15 のように区切る。count や effect は列挙名を書く。
/// </summary>
[Serializable]
public class BestiaryData
{
    public int id; //固定 ID。セーブもこの ID。雑魚は 1〜、ボスは 101〜
    public string memo; //内部のメモ（画面には出さない）
    public bool boss; //ボス。研究も効果もなく、記録だけ残す
    public BestiaryCount count; //研究の条件で数えるもの
    public int baseCount; //研究の条件の基準数。Lv n の条件は baseCount × countRates[n-1]
    public int[] countRates; //Lv ごとの撃破数（当てた回数）の倍率。1|4|15|50|150
    public BestiaryEffect effect; //研究で伸びる効果
    public float perLevel; //1 Lv あたりの効果量。0.04 で +4%
    public long[] costs; //Lv ごとの値段（DD）。1|4|15|50|150

    /// <summary>研究できる最大の Lv。ボスは 0。</summary>
    public int MaxLevel
    {
        get
        {
            if (boss) return 0;
            int rates = countRates != null ? countRates.Length : 0;
            int prices = costs != null ? costs.Length : 0;
            return Mathf.Min(rates, prices);
        }
    }

    /// <summary>Lv level（1 始まり）を研究するのに要る数。</summary>
    public long Requirement(int level)
    {
        if (level < 1 || level > MaxLevel) return long.MaxValue;
        return (long)Mathf.Max(0, baseCount) * Mathf.Max(0, countRates[level - 1]);
    }

    /// <summary>Lv level（1 始まり）を研究する値段（DD）。</summary>
    public long Cost(int level)
    {
        if (level < 1 || level > MaxLevel) return long.MaxValue;
        return Math.Max(0L, costs[level - 1]);
    }
}

/// <summary>
/// ゾンビ図鑑の項目の一覧。Resources/SO/SO_BestiaryData から読む。
/// 名前と観察日記は SO_Locale のキー（bestiary.&lt;id&gt;.name、bestiary.&lt;id&gt;.diary0〜diary5）。
/// </summary>
[CreateAssetMenu(menuName = "SO/BestiaryData")]
public class SO_BestiaryData : ScriptableObject
{
    public BestiaryData[] entries = Array.Empty<BestiaryData>();

    Dictionary<int, BestiaryData> map;

    public BestiaryData Find(int id)
    {
        if (map == null)
        {
            map = new Dictionary<int, BestiaryData>();
            if (entries != null)
            {
                foreach (var entry in entries)
                    if (entry != null) map[entry.id] = entry;
            }
        }
        map.TryGetValue(id, out var found);
        return found;
    }

    public void ApplyImported(BestiaryData[] imported)
    {
        if (imported == null || imported.Length == 0)
        {
            Debug.LogWarning("Bestiary シートが空なので、今の中身を残します。");
            return;
        }
        entries = imported;
        map = null;
    }

    void OnValidate()
    {
        map = null;
    }

    /// <summary>仕様書（LevelDesign/Bestiary.md）の仮の値で埋める。</summary>
    [ContextMenu("仕様書の初期値で埋める")]
    public void FillDefaults()
    {
        int[] rates = { 1, 4, 15, 50, 150 };
        long[] prices = { 1, 4, 15, 50, 150 };
        BestiaryData Mob(int id, string memo, int baseCount, BestiaryEffect effect, float perLevel, BestiaryCount count = BestiaryCount.Kill)
        {
            return new BestiaryData
            {
                id = id, memo = memo, count = count, baseCount = baseCount,
                countRates = (int[])rates.Clone(), effect = effect, perLevel = perLevel, costs = (long[])prices.Clone(),
            };
        }
        BestiaryData Boss(int id, string memo)
        {
            return new BestiaryData { id = id, memo = memo, boss = true, countRates = Array.Empty<int>(), costs = Array.Empty<long>() };
        }

        entries = new[]
        {
            Mob(1, "ゾンビ（正方形・縦長・横長）", 50, BestiaryEffect.NormalDamage, 0.04f),
            Mob(2, "装甲ゾンビ", 20, BestiaryEffect.ArmorPierce, 0.05f),
            Mob(3, "再生ゾンビ", 20, BestiaryEffect.RegenDelay, 0.10f),
            Mob(4, "膨張ゾンビ", 20, BestiaryEffect.BloatRadius, 0.06f),
            Mob(5, "分裂ゾンビ（分かれた子も）", 20, BestiaryEffect.SplitHp, 0.06f),
            Mob(6, "幽霊ゾンビ", 5, BestiaryEffect.GhostSolid, 0.08f),
            Mob(7, "ガラス", 5, BestiaryEffect.GlassDamage, 0.10f),
            Mob(8, "宝箱", 5, BestiaryEffect.ChestWeight, 0.08f),
            Mob(9, "石（当てた回数）", 100, BestiaryEffect.AllDamage, 0.01f, BestiaryCount.Hit),
            Boss(101, "大ゾンビ"),
            Boss(102, "甲羅ボス"),
            Boss(103, "再生ボス"),
        };
        map = null;
    }
}
