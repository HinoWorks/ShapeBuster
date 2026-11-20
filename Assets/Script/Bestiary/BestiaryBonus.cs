using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゾンビ図鑑の研究の効果。SkillTreeBonus と同じく「1 Lv 分の効果 × 研究 Lv」を項目ごとに足して持つ。
/// 研究やセーブの読み込みで Invalidate され、次に読まれたときに作り直す。
/// ゲーム側はここのプロパティから最終の値を読む。
/// </summary>
public static class BestiaryBonus
{
    static readonly Dictionary<BestiaryEffect, float> values = new();
    static bool dirty = true;

    public static void Invalidate()
    {
        dirty = true;
    }

    /// <summary>効果の合計。0.04 は +4%。</summary>
    public static float Get(BestiaryEffect effect)
    {
        Rebuild();
        values.TryGetValue(effect, out var value);
        return value;
    }

    /// <summary>普通のゾンビへのダメージの増え分。</summary>
    public static float NormalDamage => Mathf.Max(0f, Get(BestiaryEffect.NormalDamage));
    /// <summary>装甲の殻に当たったとき、本体に通るダメージの割合（0〜1）。</summary>
    public static float ArmorPierce => Mathf.Clamp01(Get(BestiaryEffect.ArmorPierce));
    /// <summary>再生ゾンビの回復が始まるまでの時間の増え分。</summary>
    public static float RegenDelay => Mathf.Max(0f, Get(BestiaryEffect.RegenDelay));
    /// <summary>膨張ゾンビの爆発の範囲の増え分。</summary>
    public static float BloatRadius => Mathf.Max(0f, Get(BestiaryEffect.BloatRadius));
    /// <summary>分裂ゾンビの分かれた子の HP の減り分（0〜0.9）。</summary>
    public static float SplitHp => Mathf.Clamp(Get(BestiaryEffect.SplitHp), 0f, 0.9f);
    /// <summary>幽霊ゾンビが実体でいる時間の増え分。</summary>
    public static float GhostSolid => Mathf.Max(0f, Get(BestiaryEffect.GhostSolid));
    /// <summary>ガラスへのダメージの増え分。</summary>
    public static float GlassDamage => Mathf.Max(0f, Get(BestiaryEffect.GlassDamage));
    /// <summary>宝箱の出やすさの増え分。</summary>
    public static float ChestWeight => Mathf.Max(0f, Get(BestiaryEffect.ChestWeight));
    /// <summary>全ボールの攻撃力の増え分（石）。</summary>
    public static float AllDamage => Mathf.Max(0f, Get(BestiaryEffect.AllDamage));

    static void Rebuild()
    {
        if (!dirty) return;
        values.Clear();
        foreach (var state in BestiaryRuntime.All)
        {
            if (state.level <= 0) continue;
            var data = BestiaryRuntime.Data(state.id);
            if (data == null || data.boss || data.effect == BestiaryEffect.None) continue;
            int level = Mathf.Min(state.level, data.MaxLevel);
            values.TryGetValue(data.effect, out var current);
            values[data.effect] = current + data.perLevel * level;
        }
        dirty = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        values.Clear();
        dirty = true;
    }
}
