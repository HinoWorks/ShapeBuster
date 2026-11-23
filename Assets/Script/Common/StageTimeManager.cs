using UniRx;
using UnityEngine;

/// <summary>
/// ステージ経過時間。ブロックセッションの終了はデッドラインで、時間では終わらない。
/// </summary>
public class StageTimeManager : MonoBehaviour
{
    public static StageTimeManager Inst { get; private set; }

    [SerializeField] int stageIndex;

    public int StageIndex => stageIndex;
    public float stageTime { get; private set; }
    public float stageMaxTime { get; private set; }
    public int activeStageIndex { get; private set; }
    public bool isTimeRunning { get; private set; }
    public bool isStageCleared { get; private set; }
    public float Progress => stageMaxTime <= 0f ? 1f : Mathf.Clamp01(stageTime / stageMaxTime);

    bool isFailed;

    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        StageHarvest.Ensure();
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
        // 画面切り替えの幕が開ききるまでと、デッドラインに着いて場を止めている間は、ステージの時間を進めない。
        if (isTimeRunning && !BlockSession.CurtainHolds && !BlockSession.FailHolds)
            stageTime += Time.deltaTime;
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
        isStageCleared = false;
        isFailed = false;
        isTimeRunning = true;
    }

    /// <summary>ボスを倒した。次の段が選べるようにして、ランを終える。</summary>
    public void ClearByBoss()
    {
        FinishStage();
    }

    void FinishStage()
    {
        if (isStageCleared || isFailed) return;

        isStageCleared = true;
        isTimeRunning = false;
        stageTime = stageMaxTime;
        EnemyGenerateManager.Inst?.Clear();
        SaveLoader.Inst?.Request_SaveClearedStage(activeStageIndex);
        GameEvent.Stage.PublishCleared();
        GameWatcher.Inst.SetGameState(GameStateType.InGame_End);
        Debug.Log("<color=yellow> ========= Stage Clear ========= </color>");
    }

    public void SetStageIndex(int index)
    {
        stageIndex = index;
    }

    static StageData FindStageData(int index)
    {
        var stageData = SOLoader.StageData;
        if (stageData == null) return null;
        return stageData.FindStage(index);
    }





    #region -- DEBUG --
    public void Set_ForceBossPhase()
    {
        if (stageMaxTime > 0f)
            stageTime = stageMaxTime;
        FinishStage();
    }
    #endregion
}