using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public enum EffectType
{
    EnemyDead,
}

/// <summary>
/// EffectManager に登録する、種類とプレハブの組。
/// </summary>
[Serializable]
public class EffectSet
{
    public EffectType type;
    public EffectCont prefab;
    public int prewarmCount = 4;
}

/// <summary>
/// エフェクトの生成と返却。種類ごとに未使用インスタンスを持ち、Rent で借りる。
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager Inst { get; private set; }

    [SerializeField] EffectSet[] effects;

    readonly Dictionary<EffectType, Pool> pools = new();

    sealed class Pool
    {
        public EffectCont prefab;
        public EffectType type;
        public readonly Stack<EffectCont> available = new();
        public readonly List<EffectCont> active = new();
    }

    void Awake()
    {
        if (Inst == null) Inst = this;
        else { Destroy(this); return; }

        Build();
        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
            case GameStateType.InGame_End:
            case GameStateType.Title:
            case GameStateType.OutGame:
                ReleaseAll();
                break;
        }
    }

    /// <summary>指定した種類を、その場で再生して借りる。</summary>
    public EffectCont Rent(EffectType type, Vector3 worldPosition)
    {
        if (!pools.TryGetValue(type, out var pool))
        {
            Debug.LogWarning($"EffectManager: {type} のセットがありません。");
            return null;
        }

        EffectCont effect = null;
        while (pool.available.Count > 0)
        {
            effect = pool.available.Pop();
            if (effect != null) break;
        }

        if (effect == null)
            effect = Create(pool);

        pool.active.Add(effect);
        effect.MarkRented();
        effect.Play(worldPosition);
        return effect;
    }

    public void Return(EffectCont effect)
    {
        if (effect == null || effect.InPool) return;

        if (!pools.TryGetValue(effect.Type, out var pool) || !pool.active.Remove(effect))
        {
            Destroy(effect.gameObject);
            return;
        }

        effect.MarkInPool();
        effect.StopAndHide();
        effect.transform.SetParent(transform, false);
        pool.available.Push(effect);
    }

    public void ReleaseAll()
    {
        foreach (var pool in pools.Values)
        {
            for (int i = pool.active.Count - 1; i >= 0; i--)
                Return(pool.active[i]);
        }
    }

    void Build()
    {
        pools.Clear();
        if (effects == null) return;

        for (int i = 0; i < effects.Length; i++)
        {
            var set = effects[i];
            if (set == null || set.prefab == null) continue;
            if (pools.ContainsKey(set.type))
            {
                Debug.LogWarning($"EffectManager: {set.type} が重複しています。");
                continue;
            }

            var pool = new Pool
            {
                prefab = set.prefab,
                type = set.type,
            };
            pools.Add(set.type, pool);

            int count = Mathf.Max(0, set.prewarmCount);
            for (int n = 0; n < count; n++)
                pool.available.Push(Create(pool));
        }
    }

    EffectCont Create(Pool pool)
    {
        var effect = Instantiate(pool.prefab, transform);
        effect.Bind(this, pool.type);
        effect.MarkInPool();
        effect.gameObject.SetActive(false);
        return effect;
    }
}
