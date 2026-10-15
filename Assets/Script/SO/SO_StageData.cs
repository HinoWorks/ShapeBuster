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
}
