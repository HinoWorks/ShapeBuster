using UniRx;
using UnityEngine;

/// <summary>
/// ステージ経過時間。stageMaxTime まで密度を上げ、そこで止めて出現は続ける。
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
                break;
            case GameStateType.Title:
            case GameStateType.OutGame:
                isTimeRunning = false;
                break;
        }
    }



    void Update()
    {
        if (!isTimeRunning) return;
        if (stageMaxTime > 0f && stageTime >= stageMaxTime)
        {
            stageTime = stageMaxTime;
            return;
        }

        stageTime += Time.deltaTime;
        if (stageMaxTime > 0f && stageTime > stageMaxTime)
            stageTime = stageMaxTime;
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
        stageMaxTime = 0f;
        stageTime = 0f;
        isStageCleared = false;
        isTimeRunning = true;
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
    }
    #endregion
}