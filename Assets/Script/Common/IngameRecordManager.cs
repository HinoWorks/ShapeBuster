using System.Collections.Generic;
using System.Numerics;
using UniRx;
using UnityEngine;
using System;

/// <summary>
/// 1回のインゲームで溜める記録。次の Ready まで残る。
/// </summary>
public class IngameRecordData
{
    public readonly Dictionary<ResourceType, BigInteger> resourceCounts = new();
    public BigInteger enemyDefeatCount;
    public BigInteger damageDealt;
    public bool isCleared;
    public float stageTime;

    public void Clear()
    {
        resourceCounts.Clear();
        enemyDefeatCount = 0;
        damageDealt = 0;
        isCleared = false;
        stageTime = 0f;
    }

    public BigInteger GetResourceCount(ResourceType type)
    {
        return resourceCounts.TryGetValue(type, out var count) ? count : BigInteger.Zero;
    }

    public void AddResource(ResourceType type, BigInteger amount)
    {
        resourceCounts.TryGetValue(type, out var current);
        var newCount = current + amount;
        resourceCounts[type] = newCount;
        IngameRecordManager.PublishResourceMod_Ingame(type, newCount);
    }
}




/// <summary>
/// インゲーム中の獲得リソース、敵撃破数、与ダメージをキャッシュする。
/// 終了時に記録を止め、SaveLoader へ確定してリザルトを開く。
/// </summary>
public class IngameRecordManager : MonoBehaviour
{
    public static IngameRecordManager Inst { get; private set; }

    public IngameRecordData Record { get; private set; } = new();
    public bool isRecording { get; private set; }


    // インゲームでリソースが変化した時のイベント、主に1回のインゲーム中のリソース変化を通知
    private static readonly Subject<(ResourceType, System.Numerics.BigInteger)> resourceMod_Ingame = new();
    public static IObservable<(ResourceType, System.Numerics.BigInteger)> ResourceMod_Ingame => resourceMod_Ingame.AsObservable();
    public static void PublishResourceMod_Ingame(ResourceType resourceType, System.Numerics.BigInteger mod)
    {
        resourceMod_Ingame.OnNext((resourceType, mod));
    }

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
                UIManager_Ingame.Inst.ui_ResultManager.gameObject.SetActive(false);
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
    /// 記録を確定する。中身は次の InGame_Ready まで Record に残り、所持数と累計は SaveLoader が書く。
    /// </summary>
    public void Save()
    {
        if (!isRecording) return;

        var stage = StageTimeManager.Inst;
        if (stage != null)
        {
            Record.isCleared = stage.isStageCleared;
            Record.stageTime = stage.stageTime;
        }

        isRecording = false;
        if (SaveLoader.Inst != null)
            SaveLoader.Inst.Request_CommitIngameRecord(Record);
        else
            Debug.LogWarning("SaveLoader がないため、インゲーム記録を保存できません。");

        UIManager_Ingame.Inst.ui_ResultManager.Show(Record);
    }
}
