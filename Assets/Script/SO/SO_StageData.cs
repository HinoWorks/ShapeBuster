using UnityEngine;



[System.Serializable]
public class StageData
{
    public int stageIndex;
    public float enemySpawnSecond;
    public float enemySpeed;
    public float enemyHpRate;
    public float enemyResourceRate;
}

/// <summary>生成時点のステージ倍率。</summary>
public struct StageEnemyScale
{
    public float hp;
    public float speed;
    public float resource;

    public static StageEnemyScale Identity => new StageEnemyScale
    {
        hp = 1f,
        speed = 1f,
        resource = 1f,
    };

    public static int ScaleInt(int value, float rate)
    {
        if (value <= 0 || rate <= 0f) return 0;
        return Mathf.Max(0, Mathf.RoundToInt(value * rate));
    }

    /// <summary>端数は確率で1個足す。倍率1なら個数はそのまま。</summary>
    public static int ScaleResourceCount(int baseCount, float rate)
    {
        if (baseCount <= 0 || rate <= 0f) return 0;

        float scaled = baseCount * rate;
        int whole = Mathf.FloorToInt(scaled);
        if (UnityEngine.Random.value < scaled - whole)
            whole++;
        return whole;
    }
}

[CreateAssetMenu(menuName = "SO/StageData")]
public class SO_StageData : ScriptableObject
{
    public StageData[] stageDatas;

    /// <summary>stageIndex が一致するステージ。無ければ null。</summary>
    public StageData FindStage(int stageIndex)
    {
        if (stageDatas == null) return null;
        for (int i = 0; i < stageDatas.Length; i++)
        {
            var stage = stageDatas[i];
            if (stage != null && stage.stageIndex == stageIndex)
                return stage;
        }
        return null;
    }

    /// <summary>
    /// ステージに設定した倍率を返す。
    /// 0 以下の速度・体力・獲得は未設定として 1。
    /// </summary>
    public StageEnemyScale GetEnemyScale(int stageIndex)
    {
        var stage = FindStage(stageIndex);
        if (stage == null) return StageEnemyScale.Identity;

        return new StageEnemyScale
        {
            hp = stage.enemyHpRate > 0f ? stage.enemyHpRate : 1f,
            speed = stage.enemySpeed > 0f ? stage.enemySpeed : 1f,
            resource = stage.enemyResourceRate > 0f ? stage.enemyResourceRate : 1f,
        };
    }

    /// <summary>現在ステージの出現間隔。0 以下なら未設定。</summary>
    public float GetCurrentSpawnSecond()
    {
        var time = StageTimeManager.Inst;
        if (time == null) return 0f;
        var stage = FindStage(time.activeStageIndex);
        return stage != null ? stage.enemySpawnSecond : 0f;
    }

    public StageEnemyScale GetCurrentEnemyScale()
    {
        var time = StageTimeManager.Inst;
        if (time == null) return StageEnemyScale.Identity;
        return GetEnemyScale(time.activeStageIndex);
    }
}
