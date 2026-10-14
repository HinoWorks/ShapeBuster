using UnityEngine;


[System.Serializable]
public class EnemyData
{
    public int enemyIndex;
    public string enemyName;
    public Sprite icon;
    public GameObject pf;
    public int maxHp;
    public int damage;
    public float speed;
    public float rotateSpeed;
    public float criticalRate;

    public ResourceType resourceType;
    public int resourceGenerateCount;
    //public GameObject resourcePf;
}

[CreateAssetMenu(menuName = "SO/EnemyData")]
public class SO_EnemyData : ScriptableObject
{
    public EnemyData[] enemyDatas;

    public EnemyData GetEnemyData(int _enemyIndex)
    {
        return enemyDatas[_enemyIndex];
    }


    public EnemyData GetRandomEnemyData()
    {
        return enemyDatas[Random.Range(0, enemyDatas.Length)];
    }

}
