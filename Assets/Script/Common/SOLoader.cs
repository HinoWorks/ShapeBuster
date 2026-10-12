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
            if (so_cursorUnitData == null)
            {
                Debug.LogError($"SO_CursorUnitData not found");
                return null;
            }
            return so_cursorUnitData;
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
