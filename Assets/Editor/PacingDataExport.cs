using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 進行計算（LevelDesign/pacing_sim.py）が読むデータを書き出す。
/// 書き出し先はプロジェクト直下の LevelDesign/data。数値を変えたら書き出し直してから計算する。
/// </summary>
public static class PacingDataExport
{
    [MenuItem("Tools/レベルデザイン/進行計算用データを書き出す")]
    public static void Export()
    {
        string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "LevelDesign", "data");
        Directory.CreateDirectory(dir);

        var rule = SOLoader.BlockSession;
        var stage = Resources.Load<SO_StageData>("SO/SO_StageData");
        var skill = Resources.Load<SO_SkillTreeData>("SO/SO_SkillTreeData");
        if (rule == null || stage == null || skill == null)
        {
            Debug.LogError("SO が読み込めません。SO_BlockSession / SO_StageData / SO_SkillTreeData を確認してください。");
            return;
        }

        var camera = Camera.main;
        var sb = new StringBuilder();
        sb.AppendLine("BlockSession: " + JsonUtility.ToJson(rule));
        sb.AppendLine("Player: " + JsonUtility.ToJson(SO_Player.Current));
        sb.AppendLine("Ball: " + JsonUtility.ToJson(SOLoader.BallData));
        sb.AppendLine("Stage: " + JsonUtility.ToJson(stage));
        foreach (var name in new[] { "square", "wide", "tall", "solid", "armor", "regen", "bloat", "split", "ghost", "glass", "chest", "boss" })
        {
            var piece = typeof(SO_BlockSession).GetField(name)?.GetValue(rule) as BlockPiece;
            if (piece != null)
                sb.AppendLine(name + ": " + JsonUtility.ToJson(piece));
        }
        // 2 体目以降のボスは「boss + bossIndex」の名前で書き出す。
        if (rule.bosses != null)
        {
            foreach (var piece in rule.bosses)
            {
                if (piece != null)
                    sb.AppendLine("boss" + piece.BossIndex + ": " + JsonUtility.ToJson(piece));
            }
        }
        if (camera != null)
            sb.AppendLine("Camera: " + JsonUtility.ToJson(new CameraInfo { orthoSize = camera.orthographicSize, aspect = camera.aspect }));
        File.WriteAllText(Path.Combine(dir, "game.txt"), sb.ToString());

        var tsv = new StringBuilder();
        foreach (var b in skill.skillTreeBases)
            tsv.AppendLine("B\t" + b.baseSkillIndex + "\t" + b.skillName + "\t" + b.jpSkillName + "\t" + b.maxLevel + "\t" + b.unit
                + "\t" + b.paramTarget + "\t" + b.targetIndex + "\t" + b.paramStat + "\t" + b.deltaValue);
        foreach (var u in skill.skillTreeUnits)
            tsv.AppendLine("U\t" + u.skillTreeIndex + "\t" + u.level + "\t" + u.maxLevel + "\t" + u.cost_AA + "\t" + u.cost_BB
                + "\t" + u.cost_CC + "\t" + u.cost_DD + "\t" + u.cost_EE + "\t" + u.cost_FF + "\t" + u.cost_GG);
        File.WriteAllText(Path.Combine(dir, "skills.tsv"), tsv.ToString());

        Debug.Log("進行計算用データを書き出しました: " + dir);
    }

    [System.Serializable]
    struct CameraInfo
    {
        public float orthoSize;
        public float aspect;
    }
}
