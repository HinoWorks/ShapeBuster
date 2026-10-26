using System;
using UnityEngine;

/// <summary>
/// マイルストーン1件。スプレッドシート Milestone の列名とフィールド名を揃える。
/// thresholds は 10|25|50 のように区切る。track や stat は列挙名を書く。
/// </summary>
[Serializable]
public class MilestoneData
{
    public int id; //行の固定ID。セーブの受け取り段階もこのID。敵は 10000+敵index、攻撃は 20000+攻撃index、ステージは 30000+ステージindex、リソースは 40000+リソース番号。共通項目は 1〜10
    public string title; //行に出す目標の名前
    public Sprite icon; //行に出す目標のアイコン
    public string bonusLabel; //報酬欄に出すボーナスの名前
    public MilestoneTrack track; //何の累計と比べるか
    public int trackKey; //累計の内訳。全体で1本の項目は 0。敵・攻撃・ステージ・リソースはそれぞれの番号
    public long[] thresholds; //段階の目標値。小さい順。10|25|50 なら 10 で1段階、25 で2段階
    public MilestoneBonusStat stat; //受け取り時に足す効果
    public int bonusKey; //効果の対象。全体に効くものは 0。特定の敵・攻撃・ステージ・リソースはそれぞれの番号
    public float bonusPerTier; //1段階あたりの量
    public MilestoneApply apply; //量を実数で足すか、割合で足すか
    public MilestoneUnit unit; //進捗表示の単位。ゲージ計算には使わない
}

[CreateAssetMenu(menuName = "SO/MilestoneData")]
public class SO_MilestoneData : ScriptableObject
{
    public MilestoneData[] milestoneDatas;

    public MilestoneData Find(int id)
    {
        if (milestoneDatas == null) return null;

        for (int i = 0; i < milestoneDatas.Length; i++)
        {
            var data = milestoneDatas[i];
            if (data != null && data.id == id)
                return data;
        }
        return null;
    }
}
