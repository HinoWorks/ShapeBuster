/// <summary>
/// 段0から段12までの進行。
/// AAは通常破壊、BBは再生、CCは装甲が落とす。
/// </summary>
public static class BlockCampaign
{
    public const int StageCount = 13;

    public static StageData[] CreateStages()
    {
        return new[]
        {
            Stage(0, 60, 8, 0.75f, 0.65f, 1f, 100, 0, 0, 0, 0, 0),
            Stage(1, 60, 8, 0.9f, 0.75f, 1f, 70, 30, 0, 0, 0, 0),
            Stage(2, 65, 10, 1f, 0.85f, 1.15f, 50, 28, 22, 0, 0, 0),
            Stage(3, 70, 10, 1f, 0.95f, 1.25f, 48, 18, 14, 0, 20, 0),
            Stage(4, 70, 10, 1.15f, 1f, 1.4f, 42, 16, 12, 0, 10, 20),
            Stage(5, 75, 10, 1.15f, 1.25f, 1.5f, 36, 22, 22, 0, 10, 10),
            Stage(6, 75, 10, 1.35f, 1.25f, 1.7f, 30, 14, 10, 0, 23, 23),
            Stage(7, 80, 10, 1.45f, 1.3f, 2f, 34, 14, 10, 0, 12, 30),
            Stage(8, 80, 10, 1.6f, 1.35f, 2.2f, 32, 14, 10, 0, 30, 14),
            Stage(9, 80, 10, 1.8f, 1.45f, 2.5f, 36, 18, 14, 0, 16, 16),
            Stage(10, 85, 10, 2f, 1.5f, 2.8f, 28, 14, 14, 0, 22, 22),
            Stage(11, 85, 10, 2.2f, 1.65f, 3.2f, 24, 14, 14, 10, 18, 20),
            Stage(12, 90, 10, 2.6f, 1.85f, 3.6f, 18, 14, 18, 16, 17, 17),
        };
    }

    static StageData Stage(
        int index, int time, int columns, float hp, float speed, float resource,
        int square, int wide, int tall, int solid, int armor, int regen)
    {
        return new StageData
        {
            stageIndex = index,
            stageTime = time,
            blockColumns = columns,
            blockHpRate = hp,
            blockSpeedRate = speed,
            blockResourceRate = resource,
            bossIndex = 0,
            weightSquare = square,
            weightWide = wide,
            weightTall = tall,
            weightSolid = solid,
            weightArmor = armor,
            weightRegen = regen,
        };
    }
}
