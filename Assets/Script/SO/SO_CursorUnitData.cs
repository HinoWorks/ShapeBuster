using UnityEngine;
using System;



/// <summary>
/// カーソルデータ
/// </summary>
[System.Serializable]
public class CursorUnitData
{
    public int cursorIndex;
    public string cursorName;
    public Sprite icon;
    public GameObject pf;

    public int damage;
    public float attackInterval;
    public float criticalRate;
    public float resourceUpRate;
    public float size;

    [Header("銃")]
    public int magazineAmmo = 10;
    public float reloadTime = 1.2f;
}





[CreateAssetMenu(menuName = "SO/CursorUnitData")]
public class SO_CursorUnitData : ScriptableObject
{
    public CursorUnitData[] cursorUnitDatas;


    public CursorUnitData GetCursorUnitData(int _cursorIndex)
    {
        var data = Array.Find(cursorUnitDatas, x => x.cursorIndex == _cursorIndex);
        if (data == null)
        {
            Debug.LogError($"CursorUnitData not found: {_cursorIndex}");
            return null;
        }
        return data;
    }


}
