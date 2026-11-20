using UnityEngine;

public static class SOLoader
{
    private static string pathBase = "SO/";


    // =====================

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

    private static SO_Locale so_locale;
    public static SO_Locale Locale
    {
        get
        {
            if (so_locale == null)
            {
                so_locale = LoadSO<SO_Locale>(pathBase + nameof(SO_Locale));
            }
            return so_locale;
        }
    }

    private static SO_MilestoneData so_milestoneData;
    public static SO_MilestoneData MilestoneData
    {
        get
        {
            if (so_milestoneData == null)
            {
                so_milestoneData = LoadSO<SO_MilestoneData>(pathBase + nameof(SO_MilestoneData));
            }
            return so_milestoneData;
        }
    }

    private static SO_BlockLevel so_blockLevel;
    public static SO_BlockLevel BlockLevel
    {
        get
        {
            if (so_blockLevel == null)
            {
                so_blockLevel = LoadSO<SO_BlockLevel>(pathBase + nameof(SO_BlockLevel));
            }
            return so_blockLevel;
        }
    }

    private static SO_BlockSession so_blockSession;
    public static SO_BlockSession BlockSession
    {
        get
        {
            if (so_blockSession == null)
            {
                so_blockSession = LoadSO<SO_BlockSession>(pathBase + nameof(SO_BlockSession));
            }
            return so_blockSession;
        }
    }

    private static SO_BallData so_ballData;
    public static SO_BallData BallData
    {
        get
        {
            if (so_ballData == null)
            {
                so_ballData = LoadSO<SO_BallData>(pathBase + nameof(SO_BallData));
            }
            return so_ballData;
        }
    }

    private static SO_BestiaryData so_bestiaryData;
    public static SO_BestiaryData BestiaryData
    {
        get
        {
            if (so_bestiaryData == null)
            {
                so_bestiaryData = LoadSO<SO_BestiaryData>(pathBase + nameof(SO_BestiaryData));
            }
            return so_bestiaryData;
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
