using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵と、撃破時リソースの生成とプール。生存中の一覧と、プレハブごとの未使用インスタンスを持つ。
/// </summary>
public class EnemyGenerateManager : MonoBehaviour
{
    public static EnemyGenerateManager Inst { get; private set; }

    readonly Dictionary<EnemyBase, Stack<EnemyBase>> pools = new();
    readonly HashSet<EnemyBase> parked = new();
    readonly List<EnemyBase> active = new();

    readonly Dictionary<ResourceCont, Stack<ResourceCont>> resourcePools = new();
    readonly HashSet<ResourceCont> resourceParked = new();
    readonly List<ResourceCont> resourceActive = new();

    const float ResourceScatterRadius = 0.4f;

    [SerializeField] float spawnInterval = 3f;
    [SerializeField] int spawnCount = 2;
    [SerializeField] Vector2 spawnAreaMin = new Vector2(-6f, 5.5f);
    [SerializeField] Vector2 spawnAreaMax = new Vector2(6f, 6);

    bool isGenerating;
    float spawnTimer;



    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    void Update()
    {
        if (!isGenerating) return;
        if (spawnInterval <= 0f || spawnCount <= 0) return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer < spawnInterval) return;

        spawnTimer -= spawnInterval;
        GenerateEnemies(spawnCount);
    }

    public void Set_GenerateState(bool isStart)
    {
        isGenerating = isStart;
        spawnTimer = 0f;
    }

    void GenerateEnemies(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var enemyData = SOLoader.EnemyData.GetRandomEnemyData();
            var prefab = enemyData.pf;
            if (prefab == null) return;
            Spawn(enemyData);
        }
    }


    Vector3 SelectSpawnPosition()
    {
        return new Vector3(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y),
            0f);
    }


    private EnemyBase Spawn(EnemyData enemyData)
    {
        var position = SelectSpawnPosition();
        var enemyBase = enemyData.pf.GetComponent<EnemyBase>();
        if (enemyBase == null) return null;
        return Spawn(enemyBase, position, enemyData);
    }





    /// <summary>hp が 0 以下ならプレハブの maxHp を使う。</summary>
    public T Spawn<T>(T prefab, Vector3 position, EnemyData enemyData) where T : EnemyBase
    {
        if (prefab == null) return null;

        var enemy = Rent(prefab);
        enemy.Activate(position, enemyData);
        active.Add(enemy);
        return enemy;
    }

    public void Prewarm<T>(T prefab, int count) where T : EnemyBase
    {
        if (prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
            Return(Create(prefab));
    }

    public void Return(EnemyBase enemy)
    {
        if (enemy == null || !parked.Add(enemy)) return;

        active.Remove(enemy);
        enemy.MarkInPool();
        enemy.gameObject.SetActive(false);
        enemy.transform.SetParent(transform, false);

        var key = enemy.SourcePrefab;
        if (key == null)
        {
            parked.Remove(enemy);
            Destroy(enemy.gameObject);
            return;
        }

        if (!pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<EnemyBase>();
            pools[key] = stack;
        }
        stack.Push(enemy);
    }

    /// <summary>敵データの生成数・種類・プレハブに従って、撃破位置へリソースを出す。</summary>
    public void SpawnResources(Vector3 origin, EnemyData enemyData)
    {
        if (enemyData == null) return;

        int count = enemyData.resourceGenerateCount;
        var resourceData = SOLoader.ResourceData.GetResourceData(enemyData.resourceType);
        var prefab = resourceData != null ? resourceData.resourceCont : null;
        if (count <= 0 || prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            var resource = RentResource(prefab);
            resource.Activate(origin + ScatterOffset(), enemyData.resourceType);
            resourceActive.Add(resource);
        }
    }

    public void Clear()
    {
        spawnTimer = 0f;
        ReleaseActiveEnemies();
        ReleaseActiveResources();
    }

    void ReleaseActiveEnemies()
    {
        if (active.Count == 0) return;

        var snapshot = active.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] == null) continue;
            snapshot[i].Release();
        }
        active.Clear();
    }

    void ReleaseActiveResources()
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

    static Vector3 ScatterOffset()
    {
        var offset = Random.insideUnitCircle * ResourceScatterRadius;
        return new Vector3(offset.x, offset.y, 0f);
    }

    T Rent<T>(T prefab) where T : EnemyBase
    {
        if (pools.TryGetValue(prefab, out var stack))
        {
            while (stack.Count > 0)
            {
                var pooled = stack.Pop();
                parked.Remove(pooled);
                if (pooled == null) continue;
                return (T)pooled;
            }
        }

        return Create(prefab);
    }

    T Create<T>(T prefab) where T : EnemyBase
    {
        var enemy = Instantiate(prefab, transform);
        enemy.Bind(this, prefab);
        return enemy;
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

        return CreateResource(prefab);
    }

    ResourceCont CreateResource(ResourceCont prefab)
    {
        var resource = Instantiate(prefab, transform);
        resource.Bind(this, prefab);
        return resource;
    }
}
