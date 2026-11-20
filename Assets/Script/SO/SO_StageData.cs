using UnityEngine;
using System.Collections.Generic;
using System;



[System.Serializable]
public class StageData
{
    /// <summary>ステージ番号。FindStage はこの番号が一致する行を使う。</summary>
    public int stageIndex;
    /// <summary>未クリアのとき、この秒数でボスを出す。経過時間の上限にもなる。0以下はボスを出さない。</summary>
    public int stageTime;
    /// <summary>横に並べるブロック数。0以下は10列。</summary>
    public int blockColumns;
    /// <summary>ブロックHPへの倍率。経過で増える基礎HPに掛ける。0以下は1。</summary>
    public float blockHpRate = 1f;
    /// <summary>ブロック下降速度への倍率。経過で増える基礎速度に掛ける。0以下は1。</summary>
    public float blockSpeedRate = 1f;
    /// <summary>壊したブロックが落とす資源数への倍率。0以下は個数そのまま。1を超えた端数は、その確率で1個足す。</summary>
    public float blockResourceRate;
    /// <summary>出すボス。Resources/Boss のプレハブにある bossIndex と一致するものを使う。</summary>
    public int bossIndex;
    /// <summary>正方形ブロックの出現重み。8種の合計が0なら、各プレハブの重みで出す。</summary>
    public int weightSquare;
    /// <summary>横長ブロックの出現重み。</summary>
    public int weightWide;
    /// <summary>縦長ブロックの出現重み。</summary>
    public int weightTall;
    /// <summary>壊れないブロックの出現重み。</summary>
    public int weightSolid;
    /// <summary>下側が装甲のブロックの出現重み。</summary>
    public int weightArmor;
    /// <summary>再生するブロックの出現重み。</summary>
    public int weightRegen;
    /// <summary>膨張（倒すと爆発する）ブロックの出現重み。</summary>
    public int weightBloat;
    /// <summary>分裂（倒すと小さいゾンビ 2 体に分かれる）ブロックの出現重み。</summary>
    public int weightSplit;
    /// <summary>幽霊（ときどきすり抜ける）ブロックの出現重み。</summary>
    public int weightGhost;
    /// <summary>ガラス（割ると中のゾンビが出る）の出現重み。</summary>
    public int weightGlass;
    /// <summary>宝箱（HP が高く、AA を多く落とす）の出現重み。</summary>
    public int weightChest;
    /// <summary>壁の加速帯が出始める秒数（ステージ開始から）。</summary>
    public int wallBoostStart;
    /// <summary>壁の加速帯が出る確率（%）。判定は SO_BlockSession の wallGimmickInterval 秒ごと。0 なら出ない。</summary>
    public int wallBoostChance;
    /// <summary>壁の減速帯が出始める秒数（ステージ開始から）。</summary>
    public int wallSlowStart;
    /// <summary>壁の減速帯が出る確率（%）。判定は wallGimmickInterval 秒ごと。0 なら出ない。</summary>
    public int wallSlowChance;

    public bool HasBlockWeights =>
        weightSquare + weightWide + weightTall + weightSolid + weightArmor + weightRegen + weightBloat + weightSplit + weightGhost + weightGlass + weightChest > 0;

    public int WeightOf(BlockPiece prefab)
    {
        if (prefab == null || prefab.BossIndex >= 0) return 0;
        if (prefab.Regenerates) return weightRegen;
        if (prefab.ArmorsBottom) return weightArmor;
        if (prefab.Bursts) return weightBloat;
        if (prefab.Splits) return weightSplit;
        if (prefab.Ghosts) return weightGhost;
        if (prefab.ReleasesContents) return weightGlass;
        // ゾンビ図鑑の研究で、宝箱が出やすくなる。
        if (prefab.IsChest) return Mathf.RoundToInt(weightChest * (1f + BestiaryBonus.ChestWeight));
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

    public float resource;

    public static StageEnemyScale Identity => new StageEnemyScale
    {
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
    /// ステージに設定した資源倍率を、経過時間にかかわらず返す。
    /// 0 以下のときは未設定として 1。
    /// </summary>
    public StageEnemyScale GetEnemyScale(int stageIndex, float elapsedSecond)
    {
        var stage = FindStage(stageIndex);
        if (stage == null) return StageEnemyScale.Identity;

        return new StageEnemyScale
        {
            resource = stage.blockResourceRate > 0f ? stage.blockResourceRate : 1f,
        };
    }

    public StageEnemyScale GetCurrentEnemyScale()
    {
        var time = StageTimeManager.Inst;
        if (time == null) return StageEnemyScale.Identity;
        return GetEnemyScale(time.activeStageIndex, time.stageTime);
    }


}
