using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UniRx;

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

    readonly Dictionary<GlassDrop, Stack<GlassDrop>> glassPools = new();
    readonly HashSet<GlassDrop> glassParked = new();
    readonly List<GlassDrop> glassActive = new();

    const float ResourceScatterRadius = 0.4f;

    [SerializeField] Vector2 spawnAreaMin = new Vector2(-6f, 5.5f);
    [SerializeField] Vector2 spawnAreaMax = new Vector2(6f, 6);
    [SerializeField] GlassDrop glassPrefab;

    [Header("指定生成のY範囲。Xは出現範囲と同じ")]
    [SerializeField] float screenTopMinY = 2.5f;
    [SerializeField] float screenTopMaxY = 4.5f;
    [SerializeField] float screenCenterMinY = -1f;
    [SerializeField] float screenCenterMaxY = 1f;

    bool isGenerating;
    float spawnStock;
    StageLevelData currentLevel;

    readonly List<StageSpawnOrder> spawnOrders = new();
    int spawnOrderStage = int.MinValue;
    int nextSpawnOrder;




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

    private void OnGameState(GameStateType state)
    {
        switch (state)
        {
            case GameStateType.InGame_Ready:
                Clear();
                isGenerating = false;
                StageTimeCheck();
                break;
            case GameStateType.InGame:
                Set_GenerateState(true);
                break;

            case GameStateType.InGame_End:
                Set_GenerateState(false);
                Clear();
                break;
            case GameStateType.Title:
            case GameStateType.OutGame:
                Set_GenerateState(false);
                Clear();
                break;
        }
    }


    private void StageTimeCheck()
    {
        if (StageTimeManager.Inst == null) return;

        var time = StageTimeManager.Inst.stageTime;
        if (currentLevel == null || time >= currentLevel.endSecond)
        {
            currentLevel = SOLoader.StageData.GetCurrentStageLevelData(PlayingStageIndex(), time);
            //TODO: レベルデータ変更時の処理
        }
    }


    void Update()
    {
        if (!isGenerating) return;

        StageTimeCheck();
        TickSpawnOrders();
        if (currentLevel == null || currentLevel.spawnCount_forSecond <= 0) return;

        float spawnPerSecond = currentLevel.spawnCount_forSecond * (1f + Mathf.Max(0f, SkillTreeBonus.SpawnRate));
        spawnStock += spawnPerSecond * Time.deltaTime;
        while (spawnStock >= 1f)
        {
            spawnStock -= 1f;
            SpawnFromLevel(currentLevel);
        }
    }

    static int PlayingStageIndex()
    {
        return StageTimeManager.Inst != null ? StageTimeManager.Inst.StageIndex : 0;
    }

    public void Set_GenerateState(bool isStart)
    {
        isGenerating = isStart;
        spawnStock = 0f;
        currentLevel = null;
    }

    /// <summary>
    /// 出現率とは別に、敵 ID・個数・高さの帯・形を指定して出す。
    /// X は出現範囲内でランダム。帯の Y 範囲はインスペクターで指定する。
    /// size は Circle の半径、Line の長さ。angle は度。0 が右、90 が上。
    /// </summary>
    public void SpawnSpecified(int enemyIndex, int count, SpawnBand band, SpawnShape shape, float size = 0f, float angle = 0f)
    {
        if (count <= 0) return;

        ExecuteSpawnOrder(new StageSpawnOrder
        {
            enemyIndex = enemyIndex,
            count = count,
            spawnBand = band,
            shape = shape,
            size = size,
            angle = angle,
        });
    }

    void TickSpawnOrders()
    {
        if (StageTimeManager.Inst == null) return;

        int stageIndex = StageTimeManager.Inst.activeStageIndex;
        if (spawnOrderStage != stageIndex)
            BindSpawnOrders(stageIndex);

        float time = StageTimeManager.Inst.stageTime;
        while (nextSpawnOrder < spawnOrders.Count && time >= spawnOrders[nextSpawnOrder].spawnSecond)
        {
            ExecuteSpawnOrder(spawnOrders[nextSpawnOrder]);
            nextSpawnOrder++;
        }
    }

    void BindSpawnOrders(int stageIndex)
    {
        spawnOrderStage = stageIndex;
        nextSpawnOrder = 0;
        spawnOrders.Clear();

        if (SOLoader.StageData == null) return;
        SOLoader.StageData.CollectSpawnOrders(stageIndex, spawnOrders);
    }

    void ResetSpawnOrders()
    {
        spawnOrderStage = int.MinValue;
        nextSpawnOrder = 0;
        spawnOrders.Clear();
    }

    void ExecuteSpawnOrder(StageSpawnOrder order)
    {
        if (order == null || order.count <= 0) return;

        var enemyData = SOLoader.EnemyData != null ? SOLoader.EnemyData.FindEnemyData(order.enemyIndex) : null;
        if (enemyData == null || enemyData.pf == null)
        {
            Debug.LogWarning($"指定生成の敵が見つかりません。 enemyIndex: {order.enemyIndex}");
            return;
        }

        var prefab = enemyData.pf.GetComponent<EnemyBase>();
        if (prefab == null) return;

        var xRange = new Vector2(spawnAreaMin.x, spawnAreaMax.x);
        var positions = order.BuildPositions(xRange, ResolveBandY(order.spawnBand));
        bool fallAsFormation = order.shape == SpawnShape.Circle || order.shape == SpawnShape.Line;
        for (int i = 0; i < positions.Length; i++)
            Spawn(prefab, positions[i], enemyData, fallAsFormation);
    }

    Vector2 ResolveBandY(SpawnBand band)
    {
        switch (band)
        {
            case SpawnBand.ScreenTop:
                return new Vector2(screenTopMinY, screenTopMaxY);
            case SpawnBand.ScreenCenter:
                return new Vector2(screenCenterMinY, screenCenterMaxY);
            default:
                return new Vector2(spawnAreaMin.y, spawnAreaMax.y);
        }
    }

    void SpawnFromLevel(StageLevelData level)
    {
        int enemyIndex = level.PickEnemyIndex();
        var enemyDatas = SOLoader.EnemyData != null ? SOLoader.EnemyData.enemyDatas : null;
        if (enemyIndex < 0 || enemyDatas == null || enemyIndex >= enemyDatas.Length) return;

        var enemyData = SOLoader.EnemyData.GetEnemyData(enemyIndex);
        if (enemyData == null || enemyData.pf == null) return;
        Spawn(enemyData);
        TrySpawnGlass();
    }

    void TrySpawnGlass()
    {
        float chance = SkillTreeBonus.GlassSpawnRate;
        if (chance <= 0f || glassPrefab == null) return;
        if (Random.value > chance) return;

        if (!TryPickGlassResource(out var resourceType, out var icon)) return;

        var glass = RentGlass(glassPrefab);
        glass.Activate(SelectSpawnPosition(), resourceType, icon);
        glassActive.Add(glass);
    }

    static bool TryPickGlassResource(out ResourceType resourceType, out Sprite icon)
    {
        resourceType = ResourceType.AA;
        icon = null;
        var table = SOLoader.ResourceData;
        if (table == null || table.resourceDatas == null) return false;

        int count = 0;
        ResourceData picked = null;
        for (int i = 0; i < table.resourceDatas.Length; i++)
        {
            var data = table.resourceDatas[i];
            if (data == null || data.resourceCont == null) continue;
            count++;
            if (Random.Range(0, count) == 0)
                picked = data;
        }
        if (picked == null) return false;

        resourceType = picked.resourceType;
        icon = picked.icon;
        return true;
    }


    Vector3 SelectSpawnPosition()
    {
        return new Vector3(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y),
            0f);
    }

    /// <summary>生存中の敵から 1 体を等確率で選ぶ。</summary>
    public bool TryPickRandomAlive(out EnemyBase enemy)
    {
        enemy = null;
        int count = 0;
        for (int i = 0; i < active.Count; i++)
        {
            var candidate = active[i];
            if (candidate == null || !candidate.isAlive) continue;
            count++;
            if (Random.Range(0, count) == 0)
                enemy = candidate;
        }
        return enemy != null;
    }


    private EnemyBase Spawn(EnemyData enemyData)
    {
        var position = SelectSpawnPosition();
        var enemyBase = enemyData.pf.GetComponent<EnemyBase>();
        if (enemyBase == null) return null;
        return Spawn(enemyBase, position, enemyData);
    }

    /// <summary>出現範囲の中央に 1 体出す。ボス用。</summary>
    public EnemyBase SpawnBoss(EnemyData enemyData)
    {
        if (enemyData == null || enemyData.pf == null) return null;

        var enemyBase = enemyData.pf.GetComponent<EnemyBase>();
        if (enemyBase == null) return null;

        var position = new Vector3(
            (spawnAreaMin.x + spawnAreaMax.x) * 0.5f,
            (spawnAreaMin.y + spawnAreaMax.y) * 0.5f,
            0f);
        var boss = Spawn(enemyBase, position, enemyData);
        boss?.MarkAsBoss();
        return boss;
    }





    /// <summary>hp が 0 以下ならプレハブの maxHp を使う。</summary>
    public T Spawn<T>(T prefab, Vector3 position, EnemyData enemyData, bool fallAsFormation = false) where T : EnemyBase
    {
        if (prefab == null) return null;

        var enemy = Rent(prefab);
        enemy.Activate(position, enemyData, fallAsFormation);
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

        int stageIndex = StageTimeManager.Inst != null ? StageTimeManager.Inst.activeStageIndex : -1;
        float resourceRate = SOLoader.StageData != null
            ? SOLoader.StageData.GetCurrentEnemyScale().resource
            : 1f;
        int count = MilestoneBonus.ScaleDropCount(
            StageEnemyScale.ScaleResourceCount(enemyData.resourceGenerateCount, resourceRate),
            enemyData.enemyIndex,
            enemyData.resourceType,
            stageIndex);
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
        spawnStock = 0f;
        currentLevel = null;
        ResetSpawnOrders();
        ReleaseActiveEnemies();
        ReleaseActiveResources();
        ReleaseActiveGlass();
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

    void ReleaseActiveGlass()
    {
        if (glassActive.Count == 0) return;

        var snapshot = glassActive.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] == null) continue;
            snapshot[i].Release();
        }
        glassActive.Clear();
    }

    /// <summary>ガラスが割れたあとの中身。カーソルが触れると獲得する。</summary>
    public void SpawnLooseResource(Vector3 origin, ResourceType resourceType)
    {
        var resourceData = SOLoader.ResourceData != null
            ? SOLoader.ResourceData.GetResourceData(resourceType)
            : null;
        var prefab = resourceData != null ? resourceData.resourceCont : null;
        if (prefab == null) return;

        var resource = RentResource(prefab);
        resource.Activate(origin, resourceType);
        resource.transform.localScale = Vector3.one * 0.35f;
        resource.transform.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack).SetTarget(resource.transform);
        resourceActive.Add(resource);
    }

    public void Return(GlassDrop glass)
    {
        if (glass == null || !glassParked.Add(glass)) return;

        glassActive.Remove(glass);
        glass.MarkInPool();
        glass.gameObject.SetActive(false);
        glass.transform.SetParent(transform, false);

        var key = glass.SourcePrefab;
        if (key == null)
        {
            glassParked.Remove(glass);
            Destroy(glass.gameObject);
            return;
        }

        if (!glassPools.TryGetValue(key, out var stack))
        {
            stack = new Stack<GlassDrop>();
            glassPools[key] = stack;
        }
        stack.Push(glass);
    }

    GlassDrop RentGlass(GlassDrop prefab)
    {
        if (glassPools.TryGetValue(prefab, out var stack))
        {
            while (stack.Count > 0)
            {
                var pooled = stack.Pop();
                glassParked.Remove(pooled);
                if (pooled == null) continue;
                return pooled;
            }
        }

        var created = Instantiate(prefab, transform);
        created.Bind(this, prefab);
        return created;
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
