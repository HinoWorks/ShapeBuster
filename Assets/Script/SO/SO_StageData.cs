using UnityEngine;
using System.Collections.Generic;
using System;



[System.Serializable]
public class StageData
{
    public int stageIndex;
    /// <summary>未クリアのとき、この秒数でボスを出す。0以下は出さない。</summary>
    public int stageTime;
    /// <summary>横に並べるブロック数。0以下は10列。</summary>
    public int blockColumns;
    /// <summary>ブロックHPへの倍率。0以下は1。</summary>
    public float blockHpRate = 1f;
    /// <summary>ブロック下降速度への倍率。0以下は1。</summary>
    public float blockSpeedRate = 1f;
    public float enemyPowerRate;
    public float enemyHpRate;
    public float enemySpeedRate;
    public float enemyResourceRate;
    /// <summary>Resources/Boss の bossIndex と一致するプレハブを出す。</summary>
    public int bossIndex;
    /// <summary>この段に出す通常ブロックの重み。合計 0 ならプレハブの重みに戻す。</summary>
    public int weightSquare;
    public int weightWide;
    public int weightTall;
    public int weightSolid;
    public int weightArmor;
    public int weightRegen;

    public bool HasBlockWeights =>
        weightSquare + weightWide + weightTall + weightSolid + weightArmor + weightRegen > 0;

    public int WeightOf(BlockPiece prefab)
    {
        if (prefab == null || prefab.BossIndex >= 0) return 0;
        if (prefab.Regenerates) return weightRegen;
        if (prefab.ArmorsBottom) return weightArmor;
        if (prefab.IsSolid) return weightSolid;
        if (prefab.CellsY > prefab.CellsX) return weightTall;
        if (prefab.CellsX > prefab.CellsY) return weightWide;
        return weightSquare;
    }
}

/// <summary>生成時点のステージ倍率。ステージ中は経過時間で上げない。</summary>
public struct StageEnemyScale
{
    public const float TimeStepSeconds = 20f;

    public float power;
    public float hp;
    public float speed;
    public float resource;

    public static StageEnemyScale Identity => new StageEnemyScale
    {
        power = 1f,
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



    /// <summary>シートの段がキャンペーンより短いときは、12段分の組成で置き換える。</summary>
    public void ApplyBlockCampaign()
    {
        if (stageDatas != null && stageDatas.Length >= BlockCampaign.StageCount)
        {
            var last = stageDatas[BlockCampaign.StageCount - 1];
            if (last != null && last.stageIndex == BlockCampaign.StageCount - 1 && last.HasBlockWeights)
                return;
        }
        stageDatas = BlockCampaign.CreateStages();
    }

    public StageData GetStageData(int _stageIndex)
    {
        return stageDatas[_stageIndex];
    }

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
    /// ステージに設定した倍率を、経過時間にかかわらず返す。
    /// 速度とドロップ倍率が 0 以下のときは未設定として 1。
    /// </summary>
    public StageEnemyScale GetEnemyScale(int stageIndex, float elapsedSecond)
    {
        var stage = FindStage(stageIndex);
        if (stage == null) return StageEnemyScale.Identity;

        return new StageEnemyScale
        {
            power = Mathf.Max(0f, stage.enemyPowerRate),
            hp = Mathf.Max(0f, stage.enemyHpRate),
            speed = stage.enemySpeedRate > 0f ? stage.enemySpeedRate : 1f,
            resource = stage.enemyResourceRate > 0f ? stage.enemyResourceRate : 1f,
        };
    }

    public StageEnemyScale GetCurrentEnemyScale()
    {
        var time = StageTimeManager.Inst;
        if (time == null) return StageEnemyScale.Identity;
        return GetEnemyScale(time.activeStageIndex, time.stageTime);
    }


}
