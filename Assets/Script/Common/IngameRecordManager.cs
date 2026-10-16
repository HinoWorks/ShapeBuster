using System.Collections.Generic;
using System.Numerics;
using UniRx;
using UnityEngine;

/// <summary>
/// 1回のインゲームで溜める記録。次の Ready まで残る。
/// </summary>
public class IngameRecordData
{
    public readonly Dictionary<ResourceType, BigInteger> resourceCounts = new();
    public BigInteger enemyDefeatCount;
    public BigInteger damageDealt;

    public void Clear()
    {
        resourceCounts.Clear();
        enemyDefeatCount = 0;
        damageDealt = 0;
    }

    public BigInteger GetResourceCount(ResourceType type)
    {
        return resourceCounts.TryGetValue(type, out var count) ? count : BigInteger.Zero;
    }

    public void AddResource(ResourceType type, BigInteger amount)
    {
        resourceCounts.TryGetValue(type, out var current);
        resourceCounts[type] = current + amount;
    }
}




/// <summary>
/// インゲーム中の獲得リソース、敵撃破数、与ダメージをキャッシュし、終了時に確定する。
/// </summary>
public class IngameRecordManager : MonoBehaviour
{
    public static IngameRecordManager Inst { get; private set; }

    public IngameRecordData Record { get; private set; } = new();
    public bool isRecording { get; private set; }

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        GameEvent.GameState.SetGameState.Subscribe(OnGameState).AddTo(this);
        GameEvent.Stage.Cleared.Subscribe(_ => Save()).AddTo(this);
        GameEvent.UI.ResourceMod_Ingame.Subscribe(OnResourceMod).AddTo(this);
        GameEvent.InGame.DamageDealt.Subscribe(OnDamageDealt).AddTo(this);
        GameEvent.InGame.EnemyDefeated.Subscribe(_ => OnEnemyDefeated()).AddTo(this);
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
                Record.Clear();
                isRecording = true;
                break;
            case GameStateType.InGame_End:
                Save();
                break;
        }
    }

    void OnResourceMod((ResourceType type, BigInteger mod) resourceMod)
    {
        if (!isRecording || resourceMod.mod == 0) return;
        Record.AddResource(resourceMod.type, resourceMod.mod);
    }

    void OnDamageDealt(int damage)
    {
        if (!isRecording || damage <= 0) return;
        Record.damageDealt += damage;
    }

    void OnEnemyDefeated()
    {
        if (!isRecording) return;
        Record.enemyDefeatCount += 1;
    }

    /// <summary>
    /// 記録を確定する。中身は次の InGame_Ready まで Record に残る。
    /// </summary>
    public void Save()
    {
        if (!isRecording) return;
        isRecording = false;
    }
}
