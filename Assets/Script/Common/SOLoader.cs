using UnityEngine;

public static class SOLoader
{
    private static string pathBase = "SO/";


    // =====================


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
