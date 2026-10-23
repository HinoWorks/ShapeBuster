using UnityEngine;

public static class SOLoader
{
    private static string pathBase = "SO/";


    // =====================

    private static SO_CursorUnitData so_cursorUnitData;
    public static SO_CursorUnitData CursorUnitData
    {
        get
        {
            if (so_cursorUnitData == null)
            {
                so_cursorUnitData = LoadSO<SO_CursorUnitData>(pathBase + nameof(SO_CursorUnitData));
            }
            return so_cursorUnitData;
        }
    }
    private static SO_StageData so_stageData;
    public static SO_StageData StageData
    {
        get
        {
            if (so_stageData == null)
            {
                so_stageData = LoadSO<SO_StageData>(pathBase + nameof(SO_StageData));
            }
            return so_stageData;
        }
    }

    private static SO_EnemyData so_enemyData;
    public static SO_EnemyData EnemyData
    {
        get
        {
            if (so_enemyData == null)
            {
                so_enemyData = LoadSO<SO_EnemyData>(pathBase + nameof(SO_EnemyData));
            }
            return so_enemyData;
        }
    }

    private static SO_ResourceData so_resourceData;
    public static SO_ResourceData ResourceData
    {
        get
        {
            if (so_resourceData == null)
            {
                so_resourceData = LoadSO<SO_ResourceData>(pathBase + nameof(SO_ResourceData));
            }

            return so_resourceData;
        }
    }

    private static SO_AttackUnitData so_attackUnitData;
    public static SO_AttackUnitData AttackUnitData
    {
        get
        {
            if (so_attackUnitData == null)
            {
                so_attackUnitData = LoadSO<SO_AttackUnitData>(pathBase + nameof(SO_AttackUnitData));
            }
            return so_attackUnitData;
        }
    }

    private static SO_SkillTreeData so_skillTreeData;
    public static SO_SkillTreeData SkillTreeData
    {
        get
        {
            if (so_skillTreeData == null)
            {
                so_skillTreeData = LoadSO<SO_SkillTreeData>(pathBase + nameof(SO_SkillTreeData));
            }
            return so_skillTreeData;
        }
    }

    private static SO_SoundData so_soundData;
    public static SO_SoundData SoundData
    {
        get
        {
            if (so_soundData == null)
            {
                so_soundData = LoadSO<SO_SoundData>(pathBase + nameof(SO_SoundData));
            }
            return so_soundData;
        }
    }



    // ========= loc =============
    private static T LoadSO<T>(string path) where T : ScriptableObject
    {
        return Resources.Load<T>(path);
    }
}
