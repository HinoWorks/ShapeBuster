using UniRx;
using UnityEngine;

/// <summary>
/// ステージ中に倒した数。以降のドロップ量とレアの出現率を上げ、ステージを出ると戻る。
/// 20体、50体、100体で一段ずつ上がり、レア出現率は 18% で止める。
/// </summary>
public static class StageHarvest
{
    public const float ChainResourceRate = 1.5f;
    public const float RareResourceRate = 2f;
    public const float RareChanceCap = 0.18f;

    static bool listening;

    public static int DefeatCount { get; private set; }

    public static float ResourceRate
    {
        get
        {
            if (DefeatCount >= 100) return 2f;
            if (DefeatCount >= 50) return 1.5f;
            if (DefeatCount >= 20) return 1.25f;
            return 1f;
        }
    }

    public static float RareChance
    {
        get
        {
            if (DefeatCount >= 100) return RareChanceCap;
            if (DefeatCount >= 50) return 0.12f;
            if (DefeatCount >= 20) return 0.06f;
            return 0f;
        }
    }

    public static void Ensure()
    {
        if (listening) return;
        listening = true;

        GameEvent.GameState.SetGameState.Subscribe(state =>
        {
            if (state == GameStateType.InGame_Ready)
                DefeatCount = 0;
        });
        GameEvent.InGame.EnemyDefeated.Subscribe(_ => DefeatCount++);
    }
}
