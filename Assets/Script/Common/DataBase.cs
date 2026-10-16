using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataBase : MonoBehaviour
{
    /*
        public static DataBase Inst;
        void Awake()
        {
            if (Inst == null) { Inst = this; }
            else { Destroy(this); }
        }
        */

    // --- GSS Load Setting --
    //https://docs.google.com/spreadsheets/d/18r14I3b0cia4TxMk525C686vuO6sjbfO5Cd16gWzNes/edit?usp=sharing
    //https://docs.google.com/spreadsheets/d/196MZMdtOL4CrDTLsYSusSHm0Ku0qwbFzoPRsczOKKE0/edit?usp=sharing
    private string SheetID = "196MZMdtOL4CrDTLsYSusSHm0Ku0qwbFzoPRsczOKKE0";

    string tqx = "tqx=out:csv";




    [Header("GSS Load data")]
    [SerializeField] SO_EnemyData mSO_EnemyData;
    [SerializeField] SO_StageData mSO_StageData;
    [SerializeField] SO_CursorUnitData mSO_CursorUnitData;
    [SerializeField] SO_SoundData mSO_SoundData;
    public async UniTask LoadData()
    {
        //await DataLoad_CursorUnitData();
        await DataLoad_StageData();
        await DataLoad_EnemyData();


#if UNITY_EDITOR
        Debug.Log($"<color=yellow>End Master Data update!</color>");
        await UniTask.Delay(200, true);

        //EditorUtility.SetDirty(mSO_CursorUnitData);
        EditorUtility.SetDirty(mSO_StageData);
        EditorUtility.SetDirty(mSO_EnemyData);

        // -- save --
        AssetDatabase.SaveAssets();
#endif
    }


    // スキルツリーのみ更新
    public async UniTask SkillTreeData_Update()
    {
        //await DataLoad_SkillTreeData();
#if UNITY_EDITOR
        //EditorUtility.SetDirty(mSO_SkillTreeData);
#endif
    }

    private async UniTask DataLoad_StageData()
    {
        var loadData = await DataLoad("StageData");
        var convData = CSVSerializer.Deserialize<StageData>(loadData);
        mSO_StageData.stageDatas = convData;

        loadData = await DataLoad("StageLevelData");
        var convData2 = CSVSerializer.Deserialize<StageLevelData>(loadData);
        mSO_StageData.stageLevelDatas = convData2;
    }
    private async UniTask DataLoad_EnemyData()
    {
        var loadData = await DataLoad("EnemyData");
        var convData = CSVSerializer.Deserialize<EnemyData>(loadData);
        mSO_EnemyData.enemyDatas = convData;
    }


    private async UniTask DataLoad_CursorUnitData()
    {
        var loadData = await DataLoad("CursorUnit");
        var convData = CSVSerializer.Deserialize<CursorUnitData>(loadData);
        mSO_CursorUnitData.cursorUnitDatas = convData;
    }


    private async UniTask DataLoad_SoundData()
    {
        var loadData = await DataLoad("Sound");
        var convData = CSVSerializer.Deserialize<SO_SoundElement>(loadData);
        mSO_SoundData.SoundData_SE = convData;
    }



    private async UniTask<string> DataLoad(string _sheetName)
    {
        string url = "https://docs.google.com/spreadsheets/d/" + SheetID + "/gviz/tq?" + tqx + "&sheet=" + _sheetName;
        UnityWebRequest request = UnityWebRequest.Get(url);
        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError
                    || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(request.error);
        }
        else { Debug.Log(request.downloadHandler.text); }
        return request.downloadHandler.text;
    }





    /*
    private async UniTask DataLoad_EnahnceData_Ball_()
    {
        string SheetName = "EnhanceData_Ball";

        string url = "https://docs.google.com/spreadsheets/d/" + SheetID + "/gviz/tq?" + tqx + "&sheet=" + SheetName;
        UnityWebRequest request = UnityWebRequest.Get(url);
        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError
                    || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(request.error);
        }
        else { Debug.Log(request.downloadHandler.text); }

        // -- create MasterData --
        //var convData = CSVSerializer.Deserialize<HintData>(request.downloadHandler.text);
        //mSO_HintData.hintDatas = convData;
    }*/






    #region -- loader 参考 --
    /*
    [MenuItem("Tools/マスターデータビルド/ローカル環境のマスターデータを最新にする")]
    public static async void LocalBuildMasterData()
    {
        Debug.Log("Build Master Data Start");

        var (names, jsons) = await GetSpreadSheetJsons();
        var datas = await GenerateMasterDatas(names, jsons);

        if (datas == null)
        {
            EditorUtility.DisplayDialog("マスターデータビルド", $"マスターデータのビルドに失敗しました。", "OK");
            return;
        }

        await SaveMasterDatas(names, datas);

        EditorUtility.DisplayDialog("マスターデータビルド", $"マスターデータのビルド・保存が完了しました。", "OK");
    }
    private static async UniTask<string[]> GetSpreadSheetNames()
    {
        var baseUrl = $"https://sheets.googleapis.com/v4/spreadsheets/{SpreadSheetId}?key={SpreadSheetAPIKey}";
        var baseJson = await WebRequestUtil.DownloadJsonAsync(baseUrl);
        var root = JsonConvert.DeserializeObject<SpreadSheet_Root>(baseJson);
        var list = new List<string>();
        foreach (var sheet in root.Sheets)
        {
            if (!sheet.Properties.Title.Contains("ignore_")) list.Add(sheet.Properties.Title);
        }
        return list.ToArray();
    }

    private static async UniTask<(string[] names, string[] jsons)> GetSpreadSheetJsons()
    {
        var sheetNames = await GetSpreadSheetNames();

        string[] jsons = new string[sheetNames.Length];

        await UniTask.Run(() =>
        {
            Parallel.For(0, jsons.Length, i =>
            {
                var name = sheetNames[i];
                var url = $"https://sheets.googleapis.com/v4/spreadsheets/{SpreadSheetId}/values/{name}?key={SpreadSheetAPIKey}";
                jsons[i] = WebRequestUtil.DownloadJson(url);
            });
        });

        return (sheetNames, jsons);
    }

    private static async UniTask SaveMasterDatas(string[] names, MasterData[][] datas)
    {
        var init = ClientAPI.Inst;
        var hasLocalServerPath = EditorPrefs.HasKey("LocalServerPath");
        await UniTask.Run(async () =>
        {
            await UniTask.SwitchToMainThread();

            var versions = new MasterDataVersions();
            versions.Version = "0.0";
            versions.Data = new MasterDataVersion[names.Length];
            var dstPath = Path.Combine(EditorPrefs.GetString("LocalServerPath"), "master_data", "0.0");
            if (hasLocalServerPath)
            {
                if (Directory.Exists(dstPath)) Directory.Delete(dstPath, true);
                Directory.CreateDirectory(dstPath);
            }
            var assetPath = Application.dataPath;
            Parallel.For(0, datas.Length, i =>
            {
                UniTask.SwitchToMainThread();
                try
                {
                    var data = datas[i];
                    var name = names[i];
                    var root = new MasterDataRoot<MasterData>()
                    {
                        Name = name,
                        Version = "0.0",
                        Data = data,
                    };
                    var json = JsonConvert.SerializeObject(root, Formatting.Indented);
                    Debug.Log(name + " = " + json);
                    EncryptUtil.SaveObject(ClientAPI.GetMasterDataPath(name), root);
                    if (ClientAPI.NetworkEnv == "None")
                    {
                        EncryptUtil.SaveObject(Path.Combine(assetPath, "ClientApi/Resources/NoNetworkMasterData", name + ".bytes"), root);
                    }

                    if (!hasLocalServerPath)
                    {
                        Debug.LogWarning("ローカルサーバー用のマスターデータの出力先が指定されていません。[Edit]-[Preferences...]-[Local Server]-[Local server path]を指定してください。");
                    }
                    else
                    {
                        versions.Data[i] = new MasterDataVersion()
                        {
                            Name = names[i],
                        };

                        File.WriteAllText(Path.Combine(dstPath, names[i].ToLower() + ".json"), JsonConvert.SerializeObject(root));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            });
            if (hasLocalServerPath)
            {
                File.WriteAllText(Path.Combine(EditorPrefs.GetString("LocalServerPath"), "master_data", "latest_version.json"), JsonConvert.SerializeObject(versions));
            }

            EncryptUtil.SaveObject(ClientAPI.MasterVerPath, versions);
        });

        if (ClientAPI.NetworkEnv == "None")
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }*/
    #endregion

}