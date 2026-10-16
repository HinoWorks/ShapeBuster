using System;
using System.Collections.Generic;
using System.Numerics;
using Cysharp.Threading.Tasks;
using UnityEngine;


#region -- GameRecordData --
[System.Serializable]
public class GameRecordData
{
    // -- total record --
    public BigInteger total_ingameCount = 0;
    public BigInteger total_blockBreakCount = 0;
    public BigInteger total_playerLevel = 1;
    public BigInteger total_treasureCount = 0;
    public BigInteger total_playerExp = 0;
    public BigInteger total_totalDamage = 0;
    public BigInteger total_skillTreeCount = 0;
    public BigInteger total_artifactCount = 0;
    public BigInteger total_pickaxeCount = 0;
    // -- one game record --
    public BigInteger oneGame_blockBreakCount = 0;
    public BigInteger oneGame_treasureCount = 0;
    public BigInteger oneGame_playerExp = 0;
    public BigInteger oneGame_totalDamage = 0;
    public BigInteger oneGame_maxDepth = 0;
}

/// <summary>
/// GameRecordDataのセーブ用（BigIntegerをstringに変換）
/// </summary>
[System.Serializable]
public class GameRecordDataSave
{
    public string total_ingameCount;
    public string total_blockBreakCount;
    public string total_playerLevel;
    public string total_treasureCount;
    public string total_playerExp;
    public string total_totalDamage;
    public string total_skillTreeCount;
    public string total_artifactCount;
    public string total_pickaxeCount;

    public string oneGame_blockBreakCount;
    public string oneGame_treasureCount;
    public string oneGame_playerExp;
    public string oneGame_totalDamage;
    public string oneGame_maxDepth;
}
#endregion



[System.Serializable]
public class SkillTreeData
{
    public string key = "";
    public int level = 0;
}


[System.Serializable]
public class ItemData
{
    public string key = ""; //不要だが一旦保持
    public int count = 0;
}

#region -- User Settings --
[System.Serializable]
public class UserSettingsData
{
    public float volumeMaster = 100f;
    public float volumeBGM = 80f;
    public float volumeSE = 80f;
    public bool muteMaster;
    public bool muteBGM;
    public bool muteSE;

    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public int fullScreenMode = (int)FullScreenMode.FullScreenWindow;
    public int bgmIndex;
    public int settingsVersion;

    /// <summary>
    /// 現在の設定をコピーして返す。
    /// </summary>
    public UserSettingsData Copy()
    {
        return new UserSettingsData
        {
            volumeMaster = volumeMaster,
            volumeBGM = volumeBGM,
            volumeSE = volumeSE,
            muteMaster = muteMaster,
            muteBGM = muteBGM,
            muteSE = muteSE,
            resolutionWidth = resolutionWidth,
            resolutionHeight = resolutionHeight,
            fullScreenMode = fullScreenMode,
            bgmIndex = bgmIndex,
            settingsVersion = settingsVersion,
        };
    }
}
#endregion


#region -- Artifact --
[System.Serializable]
public class ArtifactData
{
    public int artifactIndex;
    public int level = 1;
}
#endregion



#region -- PlayerLevel --
[System.Serializable]
public class PlayerLevelData
{
    public int level = 1;
    public BigInteger totalExp = 0;
    public BigInteger expInCurrentLevel = 0;
    public int points = 0;
}
#endregion




public enum state
{
    InitialLoad, Idling, Doing
}

public enum SaveLoadStatus
{
    Failed,
    Success,
    NotFound,
    InvalidKey,
}


[DefaultExecutionOrder(-100)]
public class SaveLoader : MonoBehaviour
{
    public static SaveLoader Inst;
    public state currentState { get; private set; } = state.InitialLoad;


    private const string KEY_CREATE_INITIAL_DATA = "key_createInitialData"; // 初期データ作成フラグ
    private const string KEY_GAME_RECORD_DATA = "key_gameRecordData"; // ゲーム記録データ


    #region -- resource data --
    private const string KEY_RESOURCE_AA = "key_resource_aa";
    private const string KEY_RESOURCE_BB = "key_resource_bb";
    private const string KEY_RESOURCE_CC = "key_resource_cc";
    private const string KEY_RESOURCE_DD = "key_resource_dd";
    private const string KEY_RESOURCE_EE = "key_resource_ee";
    private const string KEY_RESOURCE_FF = "key_resource_ff";
    private const string KEY_RESOURCE_GG = "key_resource_gg";
    private BigInteger resourceAA;
    private BigInteger resourceBB;

    private BigInteger resourceCC;
    private BigInteger resourceDD;
    private BigInteger resourceEE;
    private BigInteger resourceFF;
    private BigInteger resourceGG;
    /// <summary>
    /// メモリ上のリソース所持数を返す。
    /// </summary>
    public BigInteger Get_ResourceCount(ResourceType _resourceType)
    {
        switch (_resourceType)
        {
            case ResourceType.AA: return resourceAA;
            case ResourceType.BB: return resourceBB;
            case ResourceType.CC: return resourceCC;
            case ResourceType.DD: return resourceDD;
            case ResourceType.EE: return resourceEE;
            case ResourceType.FF: return resourceFF;
            case ResourceType.GG: return resourceGG;
            default: return 0;
        }
    }
    #endregion



    private const string KEY_ARTIFACT_CURRENTBLOCKCOUNT = "key_artifactCurrentBlockCount"; // アーティファクト用生成ブロック数
    private int artifactCurrentBlockCount;
    private bool artifactBlockCountLoadFailed;
    public int ArtifactCurrentBlockCount { get => artifactCurrentBlockCount; }


    private const string KEY_USER_SETTINGS = "key_userSettings";

    private const string KEY_TUTORIAL_SHOWN = "key_tutorialShown";
    private List<int> tutorialShownIndices = new();
    private bool tutorialLoadFailed;

    private readonly HashSet<ResourceType> corruptResources = new();
    private readonly Queue<Func<UniTask>> allQueue = new();
    private bool isProcessingQueue;


    /// <summary>
    /// シングルトンを登録し、セーブデータを読み込む。
    /// </summary>
    void Awake()
    {
        if (Inst == null) Inst = this;
        else { Destroy(this); return; }

        currentState = state.InitialLoad;
        try
        {
            SaveData_InitialLoad();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            currentState = state.Idling;
            Debug.Log(" == SaveData_InitialLoad: End == ");
        }
    }

    /// <summary>
    /// 残りのセーブを書き出してから、シングルトン参照を外す。
    /// </summary>
    void OnDestroy()
    {
        FlushQueue();
        if (Inst == this) Inst = null;
    }

    /// <summary>
    /// バックグラウンドへ入るとき、残りのセーブを書き出す。
    /// </summary>
    void OnApplicationPause(bool pause)
    {
        if (pause) FlushQueue();
    }

    /// <summary>
    /// 終了時に残りのセーブを書き出す。
    /// </summary>
    void OnApplicationQuit()
    {
        FlushQueue();
    }



    /// <summary>
    /// 初回フラグ、リソース、アーティファクト進捗、チュートリアルをディスクから読む。
    /// </summary>
    private void SaveData_InitialLoad()
    {
        try
        {
            if (!ES3.KeyExists(KEY_CREATE_INITIAL_DATA))
            {
                InitialData_Create();
                ES3.Save(KEY_CREATE_INITIAL_DATA, true);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        resourceAA = LoadStoredResource(ResourceType.AA);
        resourceBB = LoadStoredResource(ResourceType.BB);
        resourceCC = LoadStoredResource(ResourceType.CC);
        resourceDD = LoadStoredResource(ResourceType.DD);
        resourceEE = LoadStoredResource(ResourceType.EE);
        resourceFF = LoadStoredResource(ResourceType.FF);
        resourceGG = LoadStoredResource(ResourceType.GG);

        LoadArtifactBlockCount();
        LoadTutorialShown();
    }


    /// <summary>
    /// セーブが無いときの初期データを作る。
    /// </summary>
    private void InitialData_Create()
    {
        // 初期データ作成

    }

    /// <summary>
    /// リソースを文字列から読み、壊れていれば以降の上書きを止める。
    /// </summary>
    private BigInteger LoadStoredResource(ResourceType _resourceType)
    {
        var key = GetResourceKey(_resourceType);
        if (string.IsNullOrEmpty(key)) return BigInteger.Zero;

        try
        {
            if (!ES3.KeyExists(key)) return BigInteger.Zero;
            var raw = ES3.Load<string>(key);
            if (BigInteger.TryParse(raw, out var value)) return value;
            Debug.LogError($"Resource save is corrupt: {key}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        corruptResources.Add(_resourceType);
        return BigInteger.Zero;
    }

    /// <summary>
    /// アーティファクト判定用の破壊ブロック数を読む。失敗したら上書きしない。
    /// </summary>
    private void LoadArtifactBlockCount()
    {
        artifactCurrentBlockCount = 0;
        try
        {
            if (!ES3.KeyExists(KEY_ARTIFACT_CURRENTBLOCKCOUNT)) return;
            artifactCurrentBlockCount = ES3.Load<int>(KEY_ARTIFACT_CURRENTBLOCKCOUNT);
        }
        catch (Exception exception)
        {
            artifactBlockCountLoadFailed = true;
            Debug.LogException(exception);
        }
    }

    /// <summary>
    /// 表示済みチュートリアルの一覧を読む。失敗したら上書きしない。
    /// </summary>
    private void LoadTutorialShown()
    {
        tutorialShownIndices = new List<int>();
        try
        {
            if (!ES3.KeyExists(KEY_TUTORIAL_SHOWN)) return;
            var loaded = ES3.Load<List<int>>(KEY_TUTORIAL_SHOWN);
            if (loaded == null)
            {
                tutorialLoadFailed = true;
                Debug.LogError("Tutorial save is null.");
                return;
            }
            tutorialShownIndices = loaded;
        }
        catch (Exception exception)
        {
            tutorialLoadFailed = true;
            Debug.LogException(exception);
        }
    }


    /// <summary>
    /// 指定キーをキュー経由で読む。未作成と破損を分けて返す。
    /// </summary>
    public UniTask<(SaveLoadStatus status, T data)> LoadAsync<T>(string key)
    {
        var result = (status: SaveLoadStatus.Failed, data: default(T));
        EnqueueMethod(() =>
        {
            result = LoadCore<T>(key);
            return UniTask.CompletedTask;
        });
        return UniTask.FromResult(result);
    }

    /// <summary>
    /// キーを直接読み、未作成、破損、成功を分けて返す。
    /// </summary>
    private (SaveLoadStatus status, T data) LoadCore<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("Save load key is empty.");
            return (SaveLoadStatus.InvalidKey, default);
        }

        try
        {
            if (!ES3.KeyExists(key))
                return (SaveLoadStatus.NotFound, default);

            var data = ES3.Load<T>(key);
            if (data is null)
            {
                Debug.LogError($"Save load returned null: {key}");
                return (SaveLoadStatus.Failed, default);
            }
            return (SaveLoadStatus.Success, data);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save load failed: {key}");
            Debug.LogException(exception);
            return (SaveLoadStatus.Failed, default);
        }
    }

    /// <summary>
    /// 同期処理をセーブキューへ積んで実行する。
    /// </summary>
    private void EnqueueMethod(Action saveAction)
    {
        EnqueueMethod(() =>
        {
            saveAction();
            return UniTask.CompletedTask;
        });
    }

    /// <summary>
    /// 処理をキューで実行する。実行中なら、その場で続けて走らせる。
    /// </summary>
    private void EnqueueMethod(Func<UniTask> saveAction)
    {
        if (isProcessingQueue)
        {
            RunQueueAction(saveAction);
            return;
        }

        allQueue.Enqueue(saveAction);
        DrainQueue();
    }

    /// <summary>
    /// 残っているキューをすべて実行する。
    /// </summary>
    private void FlushQueue()
    {
        DrainQueue();
    }

    /// <summary>
    /// キューが空になるまで、積んだ順に実行する。
    /// </summary>
    private void DrainQueue()
    {
        if (isProcessingQueue) return;
        isProcessingQueue = true;
        try
        {
            while (allQueue.Count > 0)
                RunQueueAction(allQueue.Dequeue());
        }
        finally
        {
            isProcessingQueue = false;
        }
    }

    /// <summary>
    /// キューの1件を実行する。例外が出ても次の処理は続ける。
    /// </summary>
    private static void RunQueueAction(Func<UniTask> action)
    {
        try
        {
            var task = action();
            if (!task.Status.IsCompleted())
            {
                Debug.LogError("SaveLoader queue action yielded. Saves stay ordered only when an action finishes synchronously.");
            }
            task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }


    #region -- resource data --
    /// <summary>
    /// リソース所持数へ差分を足して保存する。
    /// </summary>
    public void Request_SaveResource(ResourceType _resourceType, BigInteger _delta)
    {
        EnqueueMethod(() => { SavePendingResource(_resourceType, _delta); });
    }
    /// <summary>
    /// 差分を加算して保存し、UIへ所持数を通知する。破損時は書かない。
    /// </summary>
    private void SavePendingResource(ResourceType _resourceType, BigInteger _delta)
    {
        if (corruptResources.Contains(_resourceType))
        {
            Debug.LogError($"Skip resource save because loaded data is corrupt: {_resourceType}");
            return;
        }

        var key = GetResourceKey(_resourceType);
        if (string.IsNullOrEmpty(key))
            throw new ArgumentOutOfRangeException(nameof(_resourceType));

        ref var value = ref GetResourceRef(_resourceType);
        var updated = value + _delta;
        ES3.Save(key, updated.ToString());
        value = updated;
        GameEvent.UI.PublishResourceMod(_resourceType, value);
    }
    /// <summary>
    /// リソース種別のセーブキーを返す。
    /// </summary>
    private static string GetResourceKey(ResourceType _resourceType)
    {
        switch (_resourceType)
        {
            case ResourceType.AA: return KEY_RESOURCE_AA;
            case ResourceType.BB: return KEY_RESOURCE_BB;
            case ResourceType.CC: return KEY_RESOURCE_CC;
            case ResourceType.DD: return KEY_RESOURCE_DD;
            case ResourceType.EE: return KEY_RESOURCE_EE;
            case ResourceType.FF: return KEY_RESOURCE_FF;
            case ResourceType.GG: return KEY_RESOURCE_GG;
            default: return null;
        }
    }
    /// <summary>
    /// リソース所持数のメモリ参照を返す。
    /// </summary>
    private ref BigInteger GetResourceRef(ResourceType _resourceType)
    {
        switch (_resourceType)
        {
            case ResourceType.AA: return ref resourceAA;
            case ResourceType.BB: return ref resourceBB;
            case ResourceType.CC: return ref resourceCC;
            case ResourceType.DD: return ref resourceDD;
            case ResourceType.EE: return ref resourceEE;
            case ResourceType.FF: return ref resourceFF;
            case ResourceType.GG: return ref resourceGG;
            default: throw new System.ArgumentOutOfRangeException(nameof(_resourceType));
        }
    }

    /// <summary>
    /// そのリソースのセーブキーがディスクにあるかを返す。
    /// </summary>
    public bool Check_ResourceKeyExists(ResourceType _resourceType)
    {
        var key = GetResourceKey(_resourceType);
        return key != null && ES3.KeyExists(key);
    }
    #endregion


    #region -- Tutorial --
    /// <summary>
    /// 指定インデックスのチュートリアルを表示済みか返す。
    /// </summary>
    public bool IsTutorialShown(int _index)
    {
        return tutorialShownIndices != null && tutorialShownIndices.Contains(_index);
    }

    /// <summary>
    /// チュートリアルを表示済みとして保存する。
    /// </summary>
    public void Request_SaveTutorialShown(int _index)
    {
        EnqueueMethod(() => { SaveTutorialShown(_index); });
    }

    /// <summary>
    /// 未登録なら一覧へ追加して保存する。破損時は書かない。
    /// </summary>
    private void SaveTutorialShown(int _index)
    {
        if (tutorialLoadFailed)
        {
            Debug.LogError("Skip tutorial save because loaded data is corrupt.");
            return;
        }

        if (tutorialShownIndices == null) tutorialShownIndices = new List<int>();
        if (tutorialShownIndices.Contains(_index)) return;

        tutorialShownIndices.Add(_index);
        ES3.Save(KEY_TUTORIAL_SHOWN, tutorialShownIndices);
    }
    #endregion





    #region -- PlayerLevel --
    /// <summary>
    /// プレイヤーレベルを読む。無ければ null。
    /// </summary>
    public UniTask<PlayerLevelData> Get_PlayerLevelData()
    {
        PlayerLevelData result = null;
        EnqueueMethod(() =>
        {
            var loaded = LoadCore<PlayerLevelData>(GetPlayerLevelDataKey());
            if (loaded.status == SaveLoadStatus.Success)
                result = loaded.data;
        });
        return UniTask.FromResult(result);
    }
    /// <summary>
    /// プレイヤーレベルを、渡した内容で上書き保存する。
    /// </summary>
    public void Request_SavePlayerLevelData(PlayerLevelData _playerLevelData)
    {
        if (_playerLevelData == null)
        {
            Debug.LogError("Skip player level save because data is null.");
            return;
        }
        EnqueueMethod(() => { SavePlayerLevelData(_playerLevelData); });
    }
    /// <summary>
    /// プレイヤーレベルをディスクへ書く。
    /// </summary>
    private void SavePlayerLevelData(PlayerLevelData _playerLevelData)
    {
        ES3.Save(GetPlayerLevelDataKey(), _playerLevelData);
    }
    /// <summary>
    /// プレイヤーレベルのポイントへ差分を足して保存する。
    /// </summary>
    public void Request_SavePlayerLevelData(int _deltaPoints)
    {
        EnqueueMethod(() => { SavePlayerLevelData(_deltaPoints); });
    }
    /// <summary>
    /// ポイントへ差分を足して保存する。未作成なら新規、破損なら書かない。
    /// </summary>
    private void SavePlayerLevelData(int _deltaPoints)
    {
        var saveKey = GetPlayerLevelDataKey();
        var currentData = LoadCore<PlayerLevelData>(saveKey);
        PlayerLevelData data;
        if (currentData.status == SaveLoadStatus.NotFound)
        {
            data = new PlayerLevelData();
        }
        else if (currentData.status != SaveLoadStatus.Success)
        {
            Debug.LogError("Skip player level point save because loaded data is not usable.");
            return;
        }
        else
        {
            data = currentData.data;
        }

        data.points += _deltaPoints;
        ES3.Save(saveKey, data);
    }
    /// <summary>
    /// プレイヤーレベルのセーブキーを返す。
    /// </summary>
    private string GetPlayerLevelDataKey()
    {
        return "PlayerLevelData";
    }
    #endregion




    #region -- SkillTree --
    private readonly Dictionary<int, SkillTreeData> skillTreeDataCache = new();
    private readonly Dictionary<int, int> skillTreeCacheVersion = new();

    /// <summary>
    /// スキルツリーを読む。キャッシュがあればそれを返す。
    /// </summary>
    public UniTask<SkillTreeData> Get_SkillTreeData(int _skillIndex)
    {
        SkillTreeData result = null;
        EnqueueMethod(() => { result = ReadSkillTreeData(_skillIndex); });
        return UniTask.FromResult(result);
    }
    /// <summary>
    /// スキルのキャッシュ世代を返す。
    /// </summary>
    private int GetSkillTreeCacheVersion(int _skillIndex)
    {
        return skillTreeCacheVersion.TryGetValue(_skillIndex, out var version) ? version : 0;
    }
    /// <summary>
    /// キャッシュかディスクからスキルを読み、進行中のセーブを優先する。
    /// </summary>
    private SkillTreeData ReadSkillTreeData(int _skillIndex)
    {
        if (skillTreeDataCache.TryGetValue(_skillIndex, out var cached))
            return cached;

        var versionAtStart = GetSkillTreeCacheVersion(_skillIndex);
        var loaded = LoadCore<SkillTreeData>(GetSkillTreeDataKey(_skillIndex));
        if (GetSkillTreeCacheVersion(_skillIndex) != versionAtStart)
        {
            skillTreeDataCache.TryGetValue(_skillIndex, out cached);
            return cached;
        }

        if (loaded.status != SaveLoadStatus.Success)
            return null;

        skillTreeDataCache[_skillIndex] = loaded.data;
        return loaded.data;
    }
    /// <summary>
    /// スキルレベルをキャッシュして保存する。失敗したらキャッシュを捨てる。
    /// </summary>
    public void Request_SaveSkillTreeData(int _skillIndex, int _level)
    {
        var saveKey = GetSkillTreeDataKey(_skillIndex);
        var newData = new SkillTreeData()
        {
            key = saveKey,
            level = _level
        };
        var version = GetSkillTreeCacheVersion(_skillIndex) + 1;
        skillTreeCacheVersion[_skillIndex] = version;
        skillTreeDataCache[_skillIndex] = newData;

        EnqueueMethod(() =>
        {
            try
            {
                SaveSkillTreeData(newData);
            }
            catch
            {
                if (GetSkillTreeCacheVersion(_skillIndex) == version)
                {
                    skillTreeCacheVersion[_skillIndex] = version - 1;
                    skillTreeDataCache.Remove(_skillIndex);
                }
                throw;
            }
        });
    }
    /// <summary>
    /// スキルツリーをディスクへ書く。
    /// </summary>
    private void SaveSkillTreeData(SkillTreeData _newData)
    {
        ES3.Save(_newData.key, _newData);
    }
    /// <summary>
    /// スキルツリーのセーブキーを返す。
    /// </summary>
    private string GetSkillTreeDataKey(int _skillIndex)
    {
        return $"SkillTreeData-{_skillIndex}";
    }
    #endregion




    #region -- Item --
    /// <summary>
    /// アイテム所持数を読む。無ければ null。
    /// </summary>
    public UniTask<ItemData> Get_ItemData(int _itemIndex)
    {
        ItemData result = null;
        EnqueueMethod(() =>
        {
            var loaded = LoadCore<ItemData>(GetItemDataKey(_itemIndex));
            if (loaded.status == SaveLoadStatus.Success)
                result = loaded.data;
        });
        return UniTask.FromResult(result);
    }
    /// <summary>
    /// アイテム所持数を保存する。
    /// </summary>
    public void Request_SaveItemData(int _itemIndex, int _count)
    {
        EnqueueMethod(() => { SaveItemData(_itemIndex, _count); });
    }
    /// <summary>
    /// アイテム所持数をディスクへ書く。
    /// </summary>
    private void SaveItemData(int _itemIndex, int _count)
    {
        var saveKey = GetItemDataKey(_itemIndex);
        var newData = new ItemData()
        {
            key = saveKey,
            count = _count
        };
        ES3.Save(saveKey, newData);
    }
    /// <summary>
    /// アイテムのセーブキーを返す。
    /// </summary>
    private string GetItemDataKey(int _itemIndex)
    {
        return $"item-{_itemIndex}";
    }
    #endregion




    #region -- Artifact --
    /// <summary>
    /// アーティファクトを読む。無ければ null。
    /// </summary>
    public UniTask<ArtifactData> Get_ArtifactData(int _artifactIndex)
    {
        ArtifactData result = null;
        EnqueueMethod(() =>
        {
            var loaded = LoadCore<ArtifactData>(GetArtifactDataKey(_artifactIndex));
            if (loaded.status == SaveLoadStatus.Success)
                result = loaded.data;
        });
        return UniTask.FromResult(result);
    }
    /// <summary>
    /// アーティファクトのレベルを保存する。
    /// </summary>
    public void Request_SaveArtifactData(int _artifactIndex, int _level)
    {
        EnqueueMethod(() => { SaveArtifactData(_artifactIndex, _level); });
    }
    /// <summary>
    /// アーティファクトをディスクへ書く。
    /// </summary>
    private void SaveArtifactData(int _artifactIndex, int _level)
    {
        var saveKey = GetArtifactDataKey(_artifactIndex);
        var newData = new ArtifactData()
        {
            artifactIndex = _artifactIndex,
            level = _level
        };
        ES3.Save(saveKey, newData);
    }
    /// <summary>
    /// アーティファクトのセーブキーを返す。
    /// </summary>
    private string GetArtifactDataKey(int _artifactIndex)
    {
        return $"ArtifactData-{_artifactIndex}";
    }

    /// <summary>
    /// アーティファクト出現判定用に、破壊したブロック数を加算またはリセットして保存する。
    /// </summary>
    public void Request_ArtifactCurrentBlockCount(int _deltaCount, bool _isReset = false)
    {
        EnqueueMethod(() =>
        {
            if (artifactBlockCountLoadFailed)
            {
                Debug.LogError("Skip artifact block count save because loaded data is corrupt.");
                return;
            }

            var updated = _isReset ? 0 : artifactCurrentBlockCount + _deltaCount;
            ES3.Save(KEY_ARTIFACT_CURRENTBLOCKCOUNT, updated);
            artifactCurrentBlockCount = updated;
        });
    }
    #endregion




    #region -- GameRecordData --
    /// <summary>
    /// 累計と1プレイ分の記録を読む。未作成は初期値、破損は null。破損結果はセーブしない。
    /// </summary>
    public UniTask<(SaveLoadStatus status, GameRecordData data)> Get_GameRecordData()
    {
        var result = (status: SaveLoadStatus.Failed, data: (GameRecordData)null);
        EnqueueMethod(() =>
        {
            var loaded = LoadCore<GameRecordDataSave>(KEY_GAME_RECORD_DATA);
            if (loaded.status == SaveLoadStatus.NotFound)
            {
                result = (SaveLoadStatus.NotFound, new GameRecordData());
                return;
            }
            if (loaded.status != SaveLoadStatus.Success)
            {
                result = (loaded.status, null);
                return;
            }
            if (!TryGameRecordDataFromSave(loaded.data, out var data))
            {
                Debug.LogError("GameRecordData is corrupt.");
                result = (SaveLoadStatus.Failed, null);
                return;
            }
            result = (SaveLoadStatus.Success, data);
        });
        return UniTask.FromResult(result);
    }
    /// <summary>
    /// ゲーム記録をまとめて保存する。
    /// </summary>
    public void Request_SaveGameRecordData(GameRecordData _gameRecordData)
    {
        if (_gameRecordData == null)
        {
            Debug.LogError("Skip GameRecordData save because data is null.");
            return;
        }
        EnqueueMethod(() => { SaveGameRecordData(_gameRecordData); });
    }
    /// <summary>
    /// BigInteger を文字列にしてゲーム記録を書く。
    /// </summary>
    private void SaveGameRecordData(GameRecordData _gameRecordData)
    {
        var saveData = new GameRecordDataSave
        {
            total_ingameCount = _gameRecordData.total_ingameCount.ToString(),
            total_blockBreakCount = _gameRecordData.total_blockBreakCount.ToString(),
            total_playerLevel = _gameRecordData.total_playerLevel.ToString(),
            total_treasureCount = _gameRecordData.total_treasureCount.ToString(),
            total_playerExp = _gameRecordData.total_playerExp.ToString(),
            total_totalDamage = _gameRecordData.total_totalDamage.ToString(),
            total_skillTreeCount = _gameRecordData.total_skillTreeCount.ToString(),
            total_artifactCount = _gameRecordData.total_artifactCount.ToString(),
            total_pickaxeCount = _gameRecordData.total_pickaxeCount.ToString(),

            oneGame_blockBreakCount = _gameRecordData.oneGame_blockBreakCount.ToString(),
            oneGame_treasureCount = _gameRecordData.oneGame_treasureCount.ToString(),
            oneGame_playerExp = _gameRecordData.oneGame_playerExp.ToString(),
            oneGame_totalDamage = _gameRecordData.oneGame_totalDamage.ToString(),
            oneGame_maxDepth = _gameRecordData.oneGame_maxDepth.ToString(),
        };
        ES3.Save(KEY_GAME_RECORD_DATA, saveData);
    }
    /// <summary>
    /// 空文字は初期値、不正な文字は失敗として数値へ戻す。
    /// </summary>
    private static bool TryParseBigIntegerField(string raw, BigInteger emptyValue, out BigInteger value)
    {
        if (string.IsNullOrEmpty(raw))
        {
            value = emptyValue;
            return true;
        }
        return BigInteger.TryParse(raw, out value);
    }
    /// <summary>
    /// セーブ用の文字列を GameRecordData へ戻す。1項目でも不正なら失敗。
    /// </summary>
    private static bool TryGameRecordDataFromSave(GameRecordDataSave save, out GameRecordData data)
    {
        data = null;
        if (save == null) return false;
        if (!TryParseBigIntegerField(save.total_ingameCount, BigInteger.Zero, out var totalIngameCount)) return false;
        if (!TryParseBigIntegerField(save.total_blockBreakCount, BigInteger.Zero, out var totalBlockBreakCount)) return false;
        if (!TryParseBigIntegerField(save.total_playerLevel, BigInteger.One, out var totalPlayerLevel)) return false;
        if (!TryParseBigIntegerField(save.total_treasureCount, BigInteger.Zero, out var totalTreasureCount)) return false;
        if (!TryParseBigIntegerField(save.total_playerExp, BigInteger.Zero, out var totalPlayerExp)) return false;
        if (!TryParseBigIntegerField(save.total_totalDamage, BigInteger.Zero, out var totalTotalDamage)) return false;
        if (!TryParseBigIntegerField(save.total_skillTreeCount, BigInteger.Zero, out var totalSkillTreeCount)) return false;
        if (!TryParseBigIntegerField(save.total_artifactCount, BigInteger.Zero, out var totalArtifactCount)) return false;
        if (!TryParseBigIntegerField(save.total_pickaxeCount, BigInteger.Zero, out var totalPickaxeCount)) return false;
        if (!TryParseBigIntegerField(save.oneGame_blockBreakCount, BigInteger.Zero, out var oneGameBlockBreakCount)) return false;
        if (!TryParseBigIntegerField(save.oneGame_treasureCount, BigInteger.Zero, out var oneGameTreasureCount)) return false;
        if (!TryParseBigIntegerField(save.oneGame_playerExp, BigInteger.Zero, out var oneGamePlayerExp)) return false;
        if (!TryParseBigIntegerField(save.oneGame_totalDamage, BigInteger.Zero, out var oneGameTotalDamage)) return false;
        if (!TryParseBigIntegerField(save.oneGame_maxDepth, BigInteger.Zero, out var oneGameMaxDepth)) return false;

        data = new GameRecordData
        {
            total_ingameCount = totalIngameCount,
            total_blockBreakCount = totalBlockBreakCount,
            total_playerLevel = totalPlayerLevel,
            total_treasureCount = totalTreasureCount,
            total_playerExp = totalPlayerExp,
            total_totalDamage = totalTotalDamage,
            total_skillTreeCount = totalSkillTreeCount,
            total_artifactCount = totalArtifactCount,
            total_pickaxeCount = totalPickaxeCount,
            oneGame_blockBreakCount = oneGameBlockBreakCount,
            oneGame_treasureCount = oneGameTreasureCount,
            oneGame_playerExp = oneGamePlayerExp,
            oneGame_totalDamage = oneGameTotalDamage,
            oneGame_maxDepth = oneGameMaxDepth,
        };
        return true;
    }
    #endregion



    #region -- User Settings --
    /// <summary>
    /// 音量や解像度などのユーザー設定を保存する。
    /// </summary>
    public void Request_SaveUserSettings(UserSettingsData data)
    {
        if (data == null)
        {
            Debug.LogError("Skip user settings save because data is null.");
            return;
        }
        EnqueueMethod(() => ES3.Save(KEY_USER_SETTINGS, data));
    }
    #endregion
}
