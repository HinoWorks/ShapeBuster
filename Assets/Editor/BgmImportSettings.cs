using UnityEditor;

/// <summary>
/// Assets/SE/BGM に入れた曲を、Streaming（少しずつ読みながら鳴らす）で取り込む。
/// 「読み込み時に全部展開する」（DecompressOnLoad）だと、曲を始める瞬間に数分ぶんの音をまとめて展開して、ゲームがかくつく。
/// 設定を変えたいときは、ここを直す（インスペクターで変えても、取り込み直すとここの設定に戻る）。
/// </summary>
public class BgmImportSettings : AssetPostprocessor
{
    const string Folder = "Assets/SE/BGM/";

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Folder)) return;

        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.loadType = UnityEngine.AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
        importer.loadInBackground = true;
    }
}
