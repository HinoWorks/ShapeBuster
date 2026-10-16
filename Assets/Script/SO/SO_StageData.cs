using UnityEngine;
using System.Collections.Generic;
using System;



[System.Serializable]
public class StageData
{
    public int stageIndex;
    public int stageTime;
    public float enemyPowerRate;
    public float enemyHpRate;
    public int bossIndex;


}

[System.Serializable]
public class StageLevelData
{
    public int stageIndex;
    public int startSecond;
    public int endSecond;

    public int spawnCount_forSecond;

    public int rate_enemy1;
    public int rate_enemy2;
    public int rate_enemy3;
    public int rate_enemy4;
    public int rate_enemy5;
    public int rate_enemy6;
    public int rate_enemy7;
    public int rate_enemy8;
    public int rate_enemy9;

    /// <summary>rate_enemy1 から順に重み抽選し、敵配列の番号を返す。合計 0 なら -1。</summary>
    public int PickEnemyIndex()
    {
        int total =
            rate_enemy1 + rate_enemy2 + rate_enemy3 +
            rate_enemy4 + rate_enemy5 + rate_enemy6 +
            rate_enemy7 + rate_enemy8 + rate_enemy9;
        if (total <= 0) return -1;

        int roll = UnityEngine.Random.Range(0, total);
        int[] rates =
        {
            rate_enemy1, rate_enemy2, rate_enemy3,
            rate_enemy4, rate_enemy5, rate_enemy6,
            rate_enemy7, rate_enemy8, rate_enemy9
        };
        for (int i = 0; i < rates.Length; i++)
        {
            roll -= rates[i];
            if (roll < 0) return i;
        }
        return -1;
    }
}



[CreateAssetMenu(menuName = "SO/StageData")]
public class SO_StageData : ScriptableObject
{
    public StageData[] stageDatas;
    public StageLevelData[] stageLevelDatas;



    public StageData GetStageData(int _stageIndex)
    {
        return stageDatas[_stageIndex];
    }
    public StageLevelData GetStageLevelData(int _stageIndex, int _startSecond)
    {
        return Array.Find(stageLevelDatas, x => x.stageIndex == _stageIndex && x.startSecond == _startSecond);
    }

    /// <summary>startSecond 以上、endSecond 未満の区間。無ければ null。</summary>
    public StageLevelData GetCurrentStageLevelData(int stageIndex, float elapsedSecond)
    {
        if (stageLevelDatas == null) return null;

        int second = Mathf.FloorToInt(elapsedSecond);
        for (int i = 0; i < stageLevelDatas.Length; i++)
        {
            var level = stageLevelDatas[i];
            if (level == null || level.stageIndex != stageIndex) continue;
            if (second >= level.startSecond && second < level.endSecond)
                return level;
        }
        return null;
    }
}
