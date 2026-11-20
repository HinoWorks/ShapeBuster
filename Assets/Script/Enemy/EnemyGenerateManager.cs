using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UniRx;

/// <summary>
/// ブロックが落とした資源の生成とプール。
/// </summary>
public class EnemyGenerateManager : MonoBehaviour
{
    public static EnemyGenerateManager Inst { get; private set; }

    readonly Dictionary<ResourceCont, Stack<ResourceCont>> resourcePools = new();
    readonly HashSet<ResourceCont> resourceParked = new();
    readonly List<ResourceCont> resourceActive = new();

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }
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
                Clear();
                break;
        }
    }

    /// <summary>
    /// origin に出して、target へ弧を描いて弾け飛ばす（宝箱）。飛び終わるまでは拾えない。
    /// </summary>
    public void SpawnBurstResource(Vector3 origin, Vector3 target, ResourceType resourceType, float fallSpeed, float duration, float arc)
    {
        var resource = SpawnLooseResource(origin, resourceType, fallSpeed);
        if (resource == null) return;
        resource.Burst(target, duration, arc);
    }

    public ResourceCont SpawnLooseResource(Vector3 origin, ResourceType resourceType, float fallSpeed = 1f)
    {
        var resourceData = SOLoader.ResourceData != null
            ? SOLoader.ResourceData.GetResourceData(resourceType)
            : null;
        var prefab = resourceData != null ? resourceData.resourceCont : null;
        if (prefab == null) return null;

        var resource = RentResource(prefab);
        resource.Activate(origin, resourceType, fallSpeed);
        resource.transform.localScale = Vector3.one * 0.35f;
        resource.transform.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack).SetTarget(resource.transform);
        resourceActive.Add(resource);
        return resource;
    }

    public void Clear()
    {
        if (resourceActive.Count == 0) return;

        var snapshot = resourceActive.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] == null) continue;
            snapshot[i].Release();
        }
        resourceActive.Clear();
    }

    public void Return(ResourceCont resource)
    {
        if (resource == null || !resourceParked.Add(resource)) return;

        resourceActive.Remove(resource);
        resource.MarkInPool();
        resource.gameObject.SetActive(false);
        resource.transform.SetParent(transform, false);

        var key = resource.SourcePrefab;
        if (key == null)
        {
            resourceParked.Remove(resource);
            Destroy(resource.gameObject);
            return;
        }

        if (!resourcePools.TryGetValue(key, out var stack))
        {
            stack = new Stack<ResourceCont>();
            resourcePools[key] = stack;
        }
        stack.Push(resource);
    }

    ResourceCont RentResource(ResourceCont prefab)
    {
        if (resourcePools.TryGetValue(prefab, out var stack))
        {
            while (stack.Count > 0)
            {
                var pooled = stack.Pop();
                resourceParked.Remove(pooled);
                if (pooled == null) continue;
                return pooled;
            }
        }

        var resource = Instantiate(prefab, transform);
        resource.Bind(this, prefab);
        return resource;
    }
}
