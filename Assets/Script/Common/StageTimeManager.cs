using UniRx;
using UnityEngine;

/// <summary>
/// ステージ経過時間。stageMaxTime で時間を止め、ボスを出す。ボス撃破でステージクリア。
/// </summary>
public class StageTimeManager : MonoBehaviour
{
    public static StageTimeManager Inst { get; private set; }

    [SerializeField] int stageIndex;

    public float stageTime { get; private set; }
    public float stageMaxTime { get; private set; }
    public int activeStageIndex { get; private set; }
    public bool isTimeRunning { get; private set; }
    public bool isBossPhase { get; private set; }
    public bool isStageCleared { get; private set; }

    bool isFailed;
    EnemyBase boss;

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
                break;
            case GameStateType.InGame:
                StartStage();
                break;
            case GameStateType.InGame_End:
                isTimeRunning = false;
                if (!isStageCleared)
                    isFailed = true;
                break;
            case GameStateType.Title:
            case GameStateType.OutGame:
                isTimeRunning = false;
                break;
        }
    }



    void Update()
    {
        if (isTimeRunning)
        {
            stageTime += Time.deltaTime;
            if (stageTime >= stageMaxTime)
            {
                stageTime = stageMaxTime;
                BeginBossPhase();
            }
        }

        if (isBossPhase && !isStageCleared && !isFailed && boss != null && !boss.isAlive)
            SpawnBoss();
    }

    public void StartStage()
    {
        var stageData = FindStageData(stageIndex);
        if (stageData == null)
        {
            isTimeRunning = false;
            Debug.LogWarning($"StageData がありません。 stageIndex: {stageIndex}");
            return;
        }

        activeStageIndex = stageData.stageIndex;
        stageMaxTime = Mathf.Max(0f, stageData.stageTime);
        stageTime = 0f;
        isBossPhase = false;
        isStageCleared = false;
        isFailed = false;
        boss = null;
        isTimeRunning = true;
    }

    void BeginBossPhase()
    {
        if (isBossPhase || isFailed) return;

        isBossPhase = true;
        isTimeRunning = false;
        stageTime = stageMaxTime;
        EnemyGenerateManager.Inst?.Set_GenerateState(false);
        SpawnBoss();
    }

    void SpawnBoss()
    {
        var stageData = FindStageData(stageIndex);
        if (stageData == null || EnemyGenerateManager.Inst == null) return;

        var enemyDatas = SOLoader.EnemyData != null ? SOLoader.EnemyData.enemyDatas : null;
        if (enemyDatas == null || stageData.bossIndex < 0 || stageData.bossIndex >= enemyDatas.Length)
        {
            Debug.LogWarning($"ボスが見つかりません。 bossIndex: {stageData.bossIndex}");
            return;
        }

        var enemyData = SOLoader.EnemyData.GetEnemyData(stageData.bossIndex);
        boss = EnemyGenerateManager.Inst.SpawnBoss(enemyData);
        boss?.SetDefeatedListener(OnBossDefeated);
    }

    void OnBossDefeated(EnemyBase enemy)
    {
        if (isStageCleared || isFailed || enemy != boss) return;

        isStageCleared = true;
        isTimeRunning = false;
        AttackManager.Inst?.Set_AttackState(false);
        GameEvent.Stage.PublishCleared();
        GameWatcher.Inst.SetGameState(GameStateType.InGame_End);
        Debug.Log("<color=yellow> ========= Stage Clear ========= </color>");
    }

    static StageData FindStageData(int index)
    {
        var stageData = SOLoader.StageData;
        if (stageData == null || stageData.stageDatas == null) return null;
        if (index < 0 || index >= stageData.stageDatas.Length) return null;
        return stageData.GetStageData(index);
    }





    #region -- DEBUG --
    public void Set_ForceBossPhase()
    {
        stageTime += stageMaxTime;
    }
    #endregion
}