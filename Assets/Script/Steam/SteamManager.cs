#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam とのつなぎ。起動時に自分で 1 つ作り、シーンをまたいで残る。
/// Steam が動いていない（エディタで Steam を起動していない、など）ときは Initialized が false のままで、何もしない。
/// エディタと、Steam を通さず起動したビルドの AppID は、プロジェクト直下（ビルドでは exe の横）の steam_appid.txt で決まる。
/// 今は試し用の 480（Spacewar）。本番の AppID が決まったら、steam_appid.txt と ReleaseAppId を書き換える。
/// </summary>
public class SteamManager : MonoBehaviour
{
    /// <summary>本番の AppID。0 のあいだは「Steam から起動し直す」処理をしない。</summary>
    const uint ReleaseAppId = 0;

    static SteamManager inst;
    static bool initialized;

    /// <summary>Steam につながっている。</summary>
    public static bool Initialized => initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        inst = null;
        initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("SteamManager");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<SteamManager>();
    }

#if !DISABLESTEAMWORKS
    void Awake()
    {
        if (!Packsize.Test())
        {
            Debug.LogError("[Steam] Packsize が合いません。このプラットフォーム向けの Steamworks.NET ではありません。");
            return;
        }
        if (!DllCheck.Test())
        {
            Debug.LogError("[Steam] steam_api のライブラリが古いか、見つかりません。");
            return;
        }

        try
        {
#if !UNITY_EDITOR
            // Steam を通さずに起動されたら、Steam から起動し直す。
            if (ReleaseAppId != 0 && SteamAPI.RestartAppIfNecessary(new AppId_t(ReleaseAppId)))
            {
                Application.Quit();
                return;
            }
#endif
            initialized = SteamAPI.Init();
        }
        catch (System.DllNotFoundException exception)
        {
            Debug.LogError("[Steam] steam_api のライブラリが読めません: " + exception.Message);
            initialized = false;
        }

        if (initialized)
            Debug.Log("[Steam] つながりました。");
        else
            Debug.LogWarning("[Steam] つながりませんでした。Steam が起動していないか、AppID が違います。実績は送りません。");
    }

    void Update()
    {
        if (!initialized) return;
        SteamAPI.RunCallbacks();
        SteamAchievements.Tick();
    }

    void OnDestroy()
    {
        if (inst != this) return;
        inst = null;
        if (!initialized) return;
        SteamAchievements.Tick(true);
        initialized = false;
        SteamAPI.Shutdown();
    }
#endif
}
