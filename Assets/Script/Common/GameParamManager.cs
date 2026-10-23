using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;


/// <summary>
/// ゲームの基本パラメータ
/// </summary>
public class GameBaseParam
{
    /*
    // インゲーム時間の初期値
    public float ingameTime => StaticManager.ingameTime_base + ingameTime_enhanced;
    private float ingameTime_enhanced = 0f;


    // ==== block 生成まわり ====
    public int blockGenerate_initialCount => StaticManager.blockGenerate_initialCount + blockGenerate_initialCount_enhanced;
    private int blockGenerate_initialCount_enhanced = 0;
    public float blockGenerate_createCount_deltaTime => StaticManager.blockGenerate_count_deltaTime + blockGenerate_createCount_deltaTime_enhanced;
    private float blockGenerate_createCount_deltaTime_enhanced = 0f;
    public float blockGenerate_duration => StaticManager.blockGenerate_duration - blockGenerate_duration_enhanced;
    private float blockGenerate_duration_enhanced = 0f;


    // ブロック再生成確率
    public float blockRegenRate => 0f + blockRegenRate_enhanced;
    private float blockRegenRate_enhanced = 0f;


    // == block tower 生成まわり ==
    public bool isTowerUnlock { get; private set; } = false;
    public int towerGenerate_count => 1 + towerGenerate_count_enhanced;
    private int towerGenerate_count_enhanced = 0;
    public float towerGenerate_duration => StaticManager.blockGenerate_towerDuration - towerGenerate_duration_enhanced;
    private float towerGenerate_duration_enhanced = 0f;
    public int towerGenerate_height => StaticManager.towerGenerate_height + towerGenerate_height_enhanced;
    private int towerGenerate_height_enhanced = 0;



    //ラッキーマイン - 採掘量が1.5倍になる確率x
    public float luckyMineRate => 0 + luckyMineRate_enhanced;
    private float luckyMineRate_enhanced = 0f;
    public bool isLuckyMine => UnityEngine.Random.Range(0f, 1f) < luckyMineRate;

    //ラッキーマイン時のリソース増加量
    public float luckyMineRate_ResourceUpRate => luckyMineRate_ResourceUpRate_enhanced;
    private float luckyMineRate_ResourceUpRate_enhanced = 0f;


    // インスタントシャッター(即破壊する確率)
    public float instantShatterRate => 0f + instantShatterRate_enhanced + ArtifactManager.Inst.instantShatterRate;
    private float instantShatterRate_enhanced = 0f;
    public bool isInstantShatter => UnityEngine.Random.Range(0f, 1f) < instantShatterRate;


    // ピッケルで攻撃するたび、インゲーム時間が増加する確率
    public float pickaxeAttack_AddIngameTimeRate => 0f + pickaxeAttack_AddIngameTimeRate_enhanced;
    private float pickaxeAttack_AddIngameTimeRate_enhanced = 0f;
    public bool isPickaxeAttack_AddIngameTime => UnityEngine.Random.Range(0f, 1f) < pickaxeAttack_AddIngameTimeRate;

    // 鉱石の基本増加量
    public int resourceBaseUpCount => resourceBaseUpCount_enhanced;
    private int resourceBaseUpCount_enhanced = 0;

    // 鉱石の耐久値増加量
    public float resourceHPUpRate => resourceHPUpRate_enhanced; // 獲得量を増やすたび、鉱石の耐久値が増加する。差分で管理
    private float resourceHPUpRate_enhanced = 0f;


    // 共通の獲得鉱石量増加 (倍率増加)
    public float resourceUpRate => ArtifactManager.Inst.resourceUpRate;


    //アーティファクト周りのパラメタ
    public int artifact_slotCount => artifact_slotCount_enhanced;
    private int artifact_slotCount_enhanced = 0;



    // === ピッケルの基礎パラメータ向上 (変更Rate分のみ, powerUp無し　=0f)===
    public float pickaxeBase_AttackDamage => pickaxeBase_AttackDamage_enhanced
                                                + ArtifactManager.Inst.pickaxe_damageRate
                                                + PickaxePowerManager.Inst.pickaxeAttackDamageRate;
    private float pickaxeBase_AttackDamage_enhanced = 0f;
    public float pickaxeBase_AttackInterval => Mathf.Min(0.8f, pickaxeBase_AttackInterval_enhanced
                                                + ArtifactManager.Inst.pickaxe_attackInterval
                                                + PickaxePowerManager.Inst.pickaxeAttackIntervalRate); // max80%の加速
    private float pickaxeBase_AttackInterval_enhanced = 0f;
    public float pickaxeBase_CriticalRate => pickaxeBase_CriticalRate_enhanced
                                                + ArtifactManager.Inst.pickaxe_criticalRate;
    private float pickaxeBase_CriticalRate_enhanced = 0f;
    public float pickaxeBase_ResourceUpRate => ArtifactManager.Inst.pickaxe_resourceUpRate;
    public float pickaxeBase_Size => pickaxeBase_Size_enhanced
                                                + ArtifactManager.Inst.pickaxe_sizeRate
                                                + PickaxePowerManager.Inst.pickaxeSizeRate;
    private float pickaxeBase_Size_enhanced = 0f;



    /// === ingameのsteageLevel上昇時にかかる能力 ===

    private bool stageLevelup_isUnlocked = false; // レベルアップ時のブロック破壊ボーナスが解除されているか
    public bool stageLevelup_breakBonusUnlock => stageLevelup_isUnlocked;

    private int stageLevelup_generateBlockCount_enhanced = 0; // レベルアップ時に生成するブロック数
    public int stageLevelup_generateBlockCount => 0 + stageLevelup_generateBlockCount_enhanced;

    private float stageLevelup_addTime_enhanced = 0; // レベルアップ時に増加する時間
    public float stageLevelup_addTime => 0 + stageLevelup_addTime_enhanced;

    private int stageLevelup_changeResource_enhanced = 0; // レベルアップ時にリソースを変更個数
    public int stageLevelup_changeResource => (int)(0 + stageLevelup_changeResource_enhanced);




    // === end game param ====
    public bool isStoneKing { get; private set; } = false;
    public bool isGameKing { get; private set; } = false;
    public bool isAttackKing { get; private set; } = false;


    public void Set_Param(ParamType _paramType, float _setParam, float _setParam2 = 0f)
    {
        switch (_paramType)
        {
            case ParamType.IngameTime:
                ingameTime_enhanced += _setParam;
                break;
            case ParamType.LuckyMineRate:
                luckyMineRate_enhanced += _setParam;
                break;
            case ParamType.LuckyMineRate_Resource:
                luckyMineRate_ResourceUpRate_enhanced += _setParam;
                break;
            case ParamType.InstantShatterRate:
                instantShatterRate_enhanced += _setParam;
                break;
            case ParamType.PickaxeAttack_AddIngameTimeRate:
                pickaxeAttack_AddIngameTimeRate_enhanced += _setParam;
                break;
            case ParamType.ResourceBaseUpCount:
                resourceBaseUpCount_enhanced += (int)_setParam;
                resourceHPUpRate_enhanced += _setParam2;
                break;

            // -- アーティファクトのスロット数向上 --
            case ParamType.ArtifactSlotCount:
                artifact_slotCount_enhanced += (int)_setParam;
                break;


            // -- ピッケルの基礎パラメータ向上 --
            case ParamType.Damage:
                pickaxeBase_AttackDamage_enhanced += _setParam;
                break;
            case ParamType.Interval:
                pickaxeBase_AttackInterval_enhanced += _setParam;
                break;
            case ParamType.CriticalRate:
                pickaxeBase_CriticalRate_enhanced += _setParam;
                break;
            case ParamType.Size:
                pickaxeBase_Size_enhanced += _setParam;
                break;


            // -- Block Generate Param --
            case ParamType.BlockGenerate_InitialCount:
                blockGenerate_initialCount_enhanced += (int)_setParam;
                break;
            case ParamType.BlockGenerate_Count_DeltaTime:
                blockGenerate_createCount_deltaTime_enhanced += _setParam;
                break;
            case ParamType.BlockGenerate_Duration:
                blockGenerate_duration_enhanced += _setParam;
                break;

            // -- Block Regen Rate --
            case ParamType.BlockRegenRate:
                blockRegenRate_enhanced += _setParam;
                break;

            // -- Block Tower Generate Param --
            case ParamType.TowerGenerate_Unlock:
                isTowerUnlock = true;
                break;
            case ParamType.TowerGenerate_Count:
                towerGenerate_count_enhanced += (int)_setParam;
                break;
            case ParamType.TowerGenerate_Duration:
                towerGenerate_duration_enhanced += _setParam;
                break;
            case ParamType.TowerGenerate_Height:
                towerGenerate_height_enhanced += (int)_setParam;
                break;


            // -- stage levelup param --
            case ParamType.StageLevelup_BreakBonusUnlock:
                stageLevelup_isUnlocked = true;
                break;
            case ParamType.StageLevelup_GenerateBlockCount:
                stageLevelup_generateBlockCount_enhanced += (int)_setParam;
                break;
            case ParamType.StageLevelup_AddTime:
                stageLevelup_addTime_enhanced += _setParam;
                break;
            case ParamType.StageLevelup_ChangeResourceCount:
                stageLevelup_changeResource_enhanced += (int)_setParam;
                break;


            // -- end game param --
            case ParamType.King_Stone:
                isStoneKing = true;
                break;
            case ParamType.King_Game:
                isGameKing = true;
                break;
            case ParamType.King_Attack:
                isAttackKing = true;
                break;
        }
    }

    public void Reset()
    {
        ingameTime_enhanced = 0f;
        blockGenerate_initialCount_enhanced = 0;
        blockGenerate_createCount_deltaTime_enhanced = 0f;
        blockGenerate_duration_enhanced = 0f;
        blockRegenRate_enhanced = 0f;
        isTowerUnlock = false;
        towerGenerate_count_enhanced = 0;
        towerGenerate_duration_enhanced = 0f;
        towerGenerate_height_enhanced = 0;
        luckyMineRate_enhanced = 0f;
        luckyMineRate_ResourceUpRate_enhanced = 0f;
        instantShatterRate_enhanced = 0f;
        pickaxeAttack_AddIngameTimeRate_enhanced = 0f;
        resourceBaseUpCount_enhanced = 0;
        resourceHPUpRate_enhanced = 0f;
        artifact_slotCount_enhanced = 0;
        pickaxeBase_AttackDamage_enhanced = 0f;
        pickaxeBase_AttackInterval_enhanced = 0f;
        pickaxeBase_CriticalRate_enhanced = 0f;
        pickaxeBase_Size_enhanced = 0f;
        stageLevelup_isUnlocked = false;
        stageLevelup_generateBlockCount_enhanced = 0;
        stageLevelup_addTime_enhanced = 0f;
        stageLevelup_changeResource_enhanced = 0;
        isStoneKing = false;
        isGameKing = false;
        isAttackKing = false;
    }
    */
}





/// <summary>
/// ピッケルのパラメータ
/// </summary>
public class CursorParam
{
    public CursorUnitData so;
    public int damage;
    public float attackInterval;
    public float criticalRate;
    public float resourceUpRate;
    public float size;

    public void Init(CursorUnitData _cursorUnitData)
    {
        so = _cursorUnitData;
        damage = _cursorUnitData.damage;
        attackInterval = _cursorUnitData.attackInterval;
        criticalRate = _cursorUnitData.criticalRate;
        resourceUpRate = _cursorUnitData.resourceUpRate;
        size = _cursorUnitData.size;
    }
}


/// <summary>
/// アタックユニットのパラメータ
/// </summary>
public class AttackParam
{
    /*
    public bool isActive { get; private set; } = false;

    public float damageRate => damageRate_enhanced + so.damageRate;
    public float attackInterval => (1f - attackInterval_enhanced) * so.attackInterval;
    public float criticalRate => criticalRate_enhanced + so.criticalRate;
    public float size => size_enhanced + so.size;
    public float aliveTime => aliveTime_enhanced + so.aliveTime;
    public float speed => so.speed + speed_enhanced;
    public int count => count_enhanced + so.count;
    public int exLevel => 1 + exLevel_enhanced;


    // -- local enhanced --
    private float damageRate_enhanced = 0f;
    private float speed_enhanced = 0f;
    private int count_enhanced = 0;
    private float attackInterval_enhanced = 0f;
    private float criticalRate_enhanced = 0f;
    private float size_enhanced = 0f;
    private int exLevel_enhanced = 0;
    private float aliveTime_enhanced = 0f;

    public void Init(AttackUnitData _attackUnitData)
    {
        so = _attackUnitData;
    }

    /*
        public void Set_Param(ParamType _paramType, float _setParam)
        {
            switch (_paramType)
            {
                case ParamType.Unlock:
                    isActive = true;
                    break;
                case ParamType.Damage:
                    damageRate_enhanced += _setParam;
                    break;
                case ParamType.Interval:
                    attackInterval_enhanced += _setParam;
                    break;
                case ParamType.Speed:
                    speed_enhanced += _setParam;
                    break;
                case ParamType.Count:
                    count_enhanced += (int)_setParam;
                    break;
                case ParamType.Size:
                    size_enhanced += _setParam;
                    break;
                case ParamType.CriticalRate:
                    criticalRate_enhanced += _setParam;
                    break;
                case ParamType.AliveTime:
                    aliveTime_enhanced += _setParam;
                    break;
                case ParamType.ExLevel:
                    exLevel_enhanced += (int)_setParam;
                    break;
            }
        }
        */
}


/// <summary>
/// ゲームのパラメータを管理するクラス
/// 全てのパラはここを参照して取得する
/// </summary>
public static class GameParamManager
{
    public readonly static GameBaseParam gameBaseParam = new GameBaseParam();
    public readonly static List<AttackParam> list_attackParam = new List<AttackParam>();
    public readonly static List<CursorParam> list_cursorParam = new List<CursorParam>();
    public static bool isInitEnd = false;



    #region ======= get param reference =======

    /*
        /// <summary>
        /// ランダムにブロックデータをタイプを抽選
        /// </summary>
        public static BlockGenerateParam Get_RandamBlockIndex() => blockChangeRateParam.SelectBlockType();
        /// <summary>
        /// ランダムな鉱石タイプを抽選(必ず鉄以上になる)
        /// </summary>
        public static BlockGenerateParam Get_RandamBlockIndex_OverIronUp() => blockChangeRateParam.SelectBlockType(false);
        /// <summary>
        /// 土を除いた鉱石タイプだけを抽選。鉱石未解放なら null
        /// </summary>
        public static BlockGenerateParam Get_RandamOreBlockIndex() => blockChangeRateParam.SelectOreBlockType();

        /// <summary>
        /// リソースがmax鉱石に変化するかチェック
        /// </summary>
        public static bool IsMaxResource(ResourceType _resourceType) => blockChangeRateParam.IsMaxResource(_resourceType);
        /// <summary>
        /// 鉱石の共通の基本増加量を取得
        /// </summary>
        public static int Get_ResourceBaseUpCount() => gameBaseParam.resourceBaseUpCount;
        /// <summary>
        /// 鉱石の耐久値増加量を取得
        /// </summary>
        public static float Get_ResourceHPUpRate() => gameBaseParam.resourceHPUpRate;
        /// <summary>
        /// 鉱石の個別の基本増加量を取得
        /// </summary>
        public static int Get_ResourceUpCount(ResourceType _resourceType) => blockChangeRateParam.Get_ResourceUpCount(_resourceType);
        /// <summary>
        /// ピッケルで攻撃するたび、インゲーム時間が増加するかチェック
        /// </summary>  
        public static bool IsPickaxeAttack_AddIngameTime() => gameBaseParam.isPickaxeAttack_AddIngameTime;
        public static AttackParam Get_AttackParam(int _attackIndex)
        {
            var targetAttack = list_attackParam.Find(x => x.attackUnitIndex == _attackIndex);
            if (targetAttack == null)
            {
                Debug.LogError($"AttackUnitData is not found: {_attackIndex} // ==> 初期ロードで読み込み失敗");
            }
            return targetAttack;
        }
        public static SubSkillParam Get_SubSkillParam(int _subSkillIndex)
        {
            var targetSubSkill = list_subSkillParam.Find(x => x.so.subSkillIndex == _subSkillIndex);
            if (targetSubSkill == null)
            {
                Debug.LogError($"SubSkillUnitData is not found: {_subSkillIndex} // ==> 初期ロードで読み込み失敗");
            }
            return targetSubSkill;
        }
        */
    public static CursorParam Get_CursorParam(int _cursorIndex)
    {
        var targetCursor = list_cursorParam.Find(x => x.so.cursorIndex == _cursorIndex);
        if (targetCursor == null)
        {
            Debug.LogError($"CursorUnitData is not found: {_cursorIndex} // ==> 初期ロードで読み込み失敗");
        }
        return targetCursor;
    }

    #endregion




    #region ======== MAIN Method========
    public static async void Init()
    {
        // ゲームの基本的なパラメタを読み込む
        Init_GameBaseParam();

        await Init_SkillTreeParam(); // skill treeによるデータ更新
        await UniTask.DelayFrame(1);

        isInitEnd = true;
    }

    /// <summary>
    /// インゲーム開始時に更新するパラメータはここに記載
    /// </summary>
    public static void Init_IngameStart()
    {
        //artifactGenerateRateParam.Init();
    }

    public static void Init_GameBaseParam()
    {
        /*
        // block generate param init
        list_blockGenerateParam.Clear();
        foreach (var blockData in SOLoader.BlockData.blockDatas)
        {
            var blockParam = new BlockBaseParam();
            blockParam.Init(blockData);
            list_blockGenerateParam.Add(blockParam);
        }

        // block change rate param init
        blockChangeRateParam.Init();

        // object generate param init
        list_objectGenerateParam.Clear();
        foreach (var objectData in SOLoader.ObjectUnitData.objectUnitDatas)
        {
            var objectParam = new ObjectGenerateParam();
            objectParam.Init(objectData);
            list_objectGenerateParam.Add(objectParam);
        }
        Set_OtherObjectRate();

        // attack param init
        list_attackParam.Clear();
        foreach (var attackData in SOLoader.AttackUnitData.attackUnitDatas)
        {
            var attackParam = new AttackParam();
            attackParam.Init(attackData);
            list_attackParam.Add(attackParam);
        }

        // sub skill param init
        list_subSkillParam.Clear();
        foreach (var subSkillData in SOLoader.SubSkillUnitData.subSkillUnitDatas)
        {
            var subSkillParam = new SubSkillParam();
            subSkillParam.Init(subSkillData);
            list_subSkillParam.Add(subSkillParam);
        }

        // pickaxe param init
        list_pickaxeParam.Clear();
        foreach (var pickaxeData in SOLoader.AttackUnitData.pickaxeUnitDatas)
        {
            var pickaxeParam = new PickaxeParam();
            pickaxeParam.Init(pickaxeData);
            list_pickaxeParam.Add(pickaxeParam);
        }
        */
    }

    private static async UniTask Init_SkillTreeParam()
    {
        SkillTreeBonus.Clear();
        if (SaveLoader.Inst == null) return;

        var table = SOLoader.SkillTreeData;
        if (table == null || table.skillTreeUnits == null) return;

        foreach (var skillData in table.skillTreeUnits)
        {
            if (skillData == null) continue;
            var saveData = await SaveLoader.Inst.Get_SkillTreeData(skillData.skillTreeIndex);
            if (saveData == null || saveData.level <= 0) continue;

            var baseSkillData = table.GetSkillTreeBaseData(skillData.baseSkillIndex);
            if (baseSkillData == null) continue;
            SkillTreeBonus.SetLevel(skillData.skillTreeIndex, baseSkillData, saveData.level);
        }
    }

    /*
        /// <summary>
        /// パラメータの差分fix
        /// </summary>
        public static void Set_DeltaParam(ParamCategory _paramCategory, int _targetIndex, ParamType _paramType, float _setParam, float _setParam2)
        {
            switch (_paramCategory)
            {
                case ParamCategory.GameSystem:
                    Set_GamesystemParam(_paramType, _setParam, _setParam2);
                    break;
                case ParamCategory.Block:
                    Set_BlockParam(_targetIndex, _paramType, _setParam);
                    break;
                case ParamCategory.BlockChangeRate:
                    Set_BlockChangeRateParam(_paramType, _targetIndex, _setParam);
                    break;
                case ParamCategory.OtherBlock:
                    Set_ObjectGenerateParam(_targetIndex, _paramType, _setParam);
                    Set_OtherObjectRate();
                    break;
                case ParamCategory.Attack:
                    Set_AttackParam(_targetIndex, _paramType, _setParam);
                    break;
                case ParamCategory.SubSkill:
                    Set_SubSkillParam(_targetIndex, _paramType, _setParam);
                    break;
            }
        }

        private static void Set_GamesystemParam(ParamType _paramType, float _setParam, float _setParam2)
        {
            gameBaseParam.Set_Param(_paramType, _setParam, _setParam2);

        }
        private static void Set_AttackParam(int _attackIndex, ParamType _paramType, float _setParam)
        {
            var targetAttack = list_attackParam.Find(x => x.attackUnitIndex == _attackIndex);
            if (targetAttack == null)
            {
                Debug.LogError($"AttackUnitData is not found: {_attackIndex} // ==> 初期ロードで読み込み失敗");
            }
            targetAttack.Set_Param(_paramType, _setParam);
        }
    */
    #endregion


}
