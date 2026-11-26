using UnityEditor;

/// <summary>
/// Tools → レベルデザイン → 総プレイ時間を表示。
/// Play 中は今数えている値、止まっているときはエディタのセーブ（key_playTime）にある値を出す。
/// </summary>
public static class PlayTimeView
{
    [MenuItem("Tools/レベルデザイン/総プレイ時間を表示")]
    static void Show()
    {
        string message;
        if (EditorApplication.isPlaying)
        {
            message = PlayTimeRecord.Summary();
        }
        else
        {
            var save = SaveLoader.PeekPlayTime();
            message = save == null
                ? "セーブに総プレイ時間の記録がありません（まだ一度も数えていません）。"
                : $"全体 {PlayTimeRecord.Format(save.totalSeconds)}（インゲーム {PlayTimeRecord.Format(save.ingameSeconds)}）";
        }
        EditorUtility.DisplayDialog("総プレイ時間", message, "OK");
    }
}
