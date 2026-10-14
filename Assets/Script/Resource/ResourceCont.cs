using UnityEngine;

/// <summary>
/// 敵を倒したときに出るリソース。生成とプールは EnemyGenerateManager が行う。
/// </summary>
[DisallowMultipleComponent]
public class ResourceCont : MonoBehaviour
{
    EnemyGenerateManager owner;
    bool inPool;

    public ResourceType ResourceType { get; private set; }
    public ResourceCont SourcePrefab { get; private set; }

    public void Bind(EnemyGenerateManager manager, ResourceCont sourcePrefab)
    {
        owner = manager;
        SourcePrefab = sourcePrefab;
    }

    public void Activate(Vector3 worldPosition, ResourceType resourceType)
    {
        inPool = false;
        ResourceType = resourceType;
        transform.position = worldPosition;
        gameObject.SetActive(true);
    }

    public void MarkInPool()
    {
        inPool = true;
    }

    /// <summary>1個獲得してプールへ戻す。プール済みなら何もしない。</summary>
    public void Collect()
    {
        if (inPool) return;

        GameEvent.UI.PublishResourceMod_Ingame(ResourceType, 1);
        Release();
    }

    /// <summary>管理者のプールへ戻す。管理者がいなければ破棄する。</summary>
    public void Release()
    {
        if (inPool) return;

        inPool = true;
        if (owner != null)
            owner.Return(this);
        else
            Destroy(gameObject);
    }
}
