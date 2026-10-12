using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;



[System.Serializable]
public class SO_SoundElement
{
    public int index;
    public string memo;
    [Tooltip("設定UIなどへの表示名。空なら memo を使う")]
    public string displayName;
    public AudioClip clip;
    [SerializeField, Range(0, 1f)] public float Volume = 1f;

    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(displayName)) return displayName;
        if (!string.IsNullOrEmpty(memo)) return memo;
        return $"BGM {index}";
    }
}


[CreateAssetMenu(menuName = "SO/SoundData")]
public class SO_SoundData : ScriptableObject
{
    [Tooltip("設定画面で切り替え可能なBGM一覧。曲を足す場合はこの配列に追加する")]
    public SO_SoundElement[] SoundData_BGM;
    public SO_SoundElement[] SoundData_SE;
    public SO_SoundElement[] SoundData_SE_UI;


    public SO_SoundElement Get_SoundData_BGM(int _index)
    {
        var data = Array.Find(SoundData_BGM, d => d.index == _index);
        return data;
    }

    /// <summary>設定UI用。clip がある BGM だけを返す</summary>
    public SO_SoundElement[] GetSelectableBGMs()
    {
        if (SoundData_BGM == null) return Array.Empty<SO_SoundElement>();
        return Array.FindAll(SoundData_BGM, d => d != null && d.clip != null);
    }



    public SO_SoundElement Get_SoundData_SE(int _index)
    {
        var data = Array.Find(SoundData_SE, d => d.index == _index);
        return data;
    }

    public SO_SoundElement Get_SoundData_SE_UI(int _index)
    {
        var data = Array.Find(SoundData_SE_UI, d => d.index == _index);
        return data;
    }

}
