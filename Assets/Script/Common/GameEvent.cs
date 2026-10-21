using Cysharp.Threading.Tasks;
using UniRx;
using System;


public enum GameStateType
{
    Title,
    InGame_Ready,
    InGame,
    InGame_End,
    Result,
    ResultEnd_ToOutGame,
    ResultEnd_ToIngameReady,
    OutGame,
}


public static class GameEvent
{

    public static class GameState
    {
        private static readonly Subject<GameStateType> setGameState = new();
        public static IObservable<GameStateType> SetGameState => setGameState.AsObservable();
        public static void PublishGameState(GameStateType state)
        {
            setGameState.OnNext(state);
        }
    }


    public static class InGame
    {
        /*
        // インゲームでゲーム記録データが変化した時のイベント、主に1回のインゲーム中の記録データ変化を通知
        private static readonly Subject<(GameRecordData_Type, System.Numerics.BigInteger)> gameRecordDataMod_Ingame = new();
        public static IObservable<(GameRecordData_Type, System.Numerics.BigInteger)> GameRecordDataMod_Ingame => gameRecordDataMod_Ingame.AsObservable();
        public static void PublishGameRecordDataMod_Ingame(GameRecordData_Type gameRecordData_Type, System.Numerics.BigInteger mod)
        {
            gameRecordDataMod_Ingame.OnNext((gameRecordData_Type, mod));
        }
*/

        /// <summary>
        /// 敵に与えたダメージ
        /// </summary>
        private static readonly Subject<int> damageDealt = new();
        public static IObservable<int> DamageDealt => damageDealt.AsObservable();
        public static void PublishDamageDealt(int damage)
        {
            damageDealt.OnNext(damage);
        }

        /// <summary>
        /// 敵を撃破した時
        /// </summary>
        private static readonly Subject<Unit> enemyDefeated = new();
        public static IObservable<Unit> EnemyDefeated => enemyDefeated.AsObservable();
        public static void PublishEnemyDefeated()
        {
            enemyDefeated.OnNext(Unit.Default);
        }

        /// <summary>
        /// HPバーに攻撃された時のイベント
        /// </summary>
        private static readonly Subject<int> hpBarDamaged = new();
        public static IObservable<int> HpBarDamaged => hpBarDamaged.AsObservable();
        public static void PublishHpBarDamaged(int damage)
        {
            hpBarDamaged.OnNext(damage);
        }



        /// <summary>
        /// アーティファクト効果発火時のイベント
        /// </summary>
        private static readonly Subject<int> artifactActiveEffect = new();
        public static IObservable<int> ArtifactActiveEffect => artifactActiveEffect.AsObservable();
        public static void PublishArtifactActiveEffect(int artifactIndex)
        {
            artifactActiveEffect.OnNext(artifactIndex);
        }


        /// <summary>
        /// インゲーム時間追加時のイベント
        /// </summary>
        private static readonly Subject<float> ingameTimeAdd = new();
        public static IObservable<float> IngameTimeAdd => ingameTimeAdd.AsObservable();
        public static void PublishIngameTimeAdd(float time)
        {
            ingameTimeAdd.OnNext(time);
        }
    }


    public static class Stage
    {
        private static readonly Subject<Unit> cleared = new();
        public static IObservable<Unit> Cleared => cleared.AsObservable();
        public static void PublishCleared()
        {
            cleared.OnNext(Unit.Default);
        }
    }


    public static class Input
    {
        private static readonly Subject<IDamagable> pointerDamage = new();
        public static IObservable<IDamagable> PointerDamage => pointerDamage.AsObservable();
        public static void PublishPointerDamage(IDamagable target)
        {
            pointerDamage.OnNext(target);
        }

        // 左クリック（主ボタン）押下
        private static readonly Subject<Unit> pointerPrimaryDown = new();
        public static IObservable<Unit> PointerPrimaryDown => pointerPrimaryDown.AsObservable();
        public static void PublishPointerPrimaryDown()
        {
            pointerPrimaryDown.OnNext(Unit.Default);
        }

        // 右クリック（副ボタン）押下
        private static readonly Subject<Unit> pointerSecondaryDown = new();
        public static IObservable<Unit> PointerSecondaryDown => pointerSecondaryDown.AsObservable();
        public static void PublishPointerSecondaryDown()
        {
            pointerSecondaryDown.OnNext(Unit.Default);
        }
    }


    public static class UI
    {
        private static readonly Subject<float> timeLimit = new();
        public static IObservable<float> TimeLimit => timeLimit.AsObservable();
        public static void PublishTimeLimit(float time)
        {
            timeLimit.OnNext(time);
        }


        // アウトゲームでリソースが変化した時発火、主に強化のフラグ管理
        private static readonly Subject<uint> resourceMod_OutGame = new();
        public static IObservable<uint> ResourceMod_OutGame => resourceMod_OutGame.AsObservable();
        public static void PublishResourceMod_OutGame()
        {
            resourceMod_OutGame.OnNext(0);
        }



        // インゲームリザルト, アウトゲームでリソースが変化した時のイベント
        private static readonly Subject<(ResourceType, System.Numerics.BigInteger)> resourceMod = new();
        public static IObservable<(ResourceType, System.Numerics.BigInteger)> ResourceMod => resourceMod.AsObservable();
        public static void PublishResourceMod(ResourceType resourceType, System.Numerics.BigInteger mod)
        {
            resourceMod.OnNext((resourceType, mod));
        }

        // インゲームでリソースが変化した時のイベント、主に1回のインゲーム中のリソース変化を通知
        private static readonly Subject<(ResourceType, System.Numerics.BigInteger)> resourceMod_Ingame = new();
        public static IObservable<(ResourceType, System.Numerics.BigInteger)> ResourceMod_Ingame => resourceMod_Ingame.AsObservable();
        public static void PublishResourceMod_Ingame(ResourceType resourceType, System.Numerics.BigInteger mod)
        {
            resourceMod_Ingame.OnNext((resourceType, mod));
        }

        private static readonly Subject<int> depthCount = new();
        public static IObservable<int> DepthCount => depthCount.AsObservable();
        public static void PublishDepthCount(int depth)
        {
            depthCount.OnNext(depth);
        }

    }

    /// <summary>メタ進行（レベル・経験値・未使用ポイント）</summary>
    public static class PlayerLevel
    {
        private static readonly Subject<(System.Numerics.BigInteger expInLevel, int level, System.Numerics.BigInteger expToNext)> playerLevelChanged = new();
        public static IObservable<(System.Numerics.BigInteger expInLevel, int level, System.Numerics.BigInteger expToNext)> PlayerLevelChanged => playerLevelChanged.AsObservable();
        public static void PublishPlayerLevelChanged(System.Numerics.BigInteger expInLevel, int level, System.Numerics.BigInteger expToNext)
        {
            playerLevelChanged.OnNext((expInLevel, level, expToNext));
        }

        private static readonly Subject<(int newLevel, int pointsGained)> levelUp = new();
        public static IObservable<(int newLevel, int pointsGained)> LevelUp => levelUp.AsObservable();
        public static void PublishPlayerLevelUp(int newLevel, int pointsGained)
        {
            levelUp.OnNext((newLevel, pointsGained));
        }
    }

    /// <summary>インゲーム中のステージレベル（ラン内リセット）</summary>
    public static class IngameStageLevel
    {
        private static readonly Subject<(int breakCountInLevel, int level, int breaksToNext)> changed = new();
        public static IObservable<(int breakCountInLevel, int level, int breaksToNext)> Changed => changed.AsObservable();
        public static void PublishChanged(int breakCountInLevel, int level, int breaksToNext)
        {
            changed.OnNext((breakCountInLevel, level, breaksToNext));
        }

        private static readonly Subject<int> levelUp = new();
        public static IObservable<int> LevelUp => levelUp.AsObservable();
        public static void PublishLevelUp(int newLevel)
        {
            levelUp.OnNext(newLevel);
        }
        private static readonly Subject<bool> isActive = new();
        public static IObservable<bool> IsActive => isActive.AsObservable();
        public static void PublishIsActive(bool _isActive)
        {
            isActive.OnNext(_isActive);
        }
    }



    public static class AchieveEvent
    {
        private static readonly Subject<Unit> skillTreeUnlock = new();
        public static IObservable<Unit> SkillTreeUnlock => skillTreeUnlock.AsObservable();
        public static void PublishSkillTreeUnlock()
        {
            skillTreeUnlock.OnNext(Unit.Default);
        }


        private static readonly Subject<Unit> pickaxeCraft = new();
        public static IObservable<Unit> PickaxeCraft => pickaxeCraft.AsObservable();
        public static void PublishPickaxeCraft()
        {
            pickaxeCraft.OnNext(Unit.Default);
        }
    }


}
