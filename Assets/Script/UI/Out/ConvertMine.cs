using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using UnityEngine;

/// <summary>
/// AAを時間をかけてDへ換える。Dはここ以外では増えない。
/// レートは固定で、EE を払うと速度が1段上がる（値段は段ごとに増える）。変換中は速度を上げられない。
/// </summary>
public static class ConvertMine
{
    /// <summary>1 AA = 0.001 D。1000 AA で 1 D。</summary>
    public const long BitsPerUnit = 1000;
    public const int MaxLevel = 36;

    public static readonly int[] DepositAmounts = { 1000, 5000, 10000, 50000, 100000, 1000000 };

    /// <summary>0段は毎秒5。36段で毎秒1,280,000。それ以上は上がらない。</summary>
    public static readonly int[] SpeedByLevel =
    {
        5, 10, 20, 40, 80, 160, 320, 640, 1280,
        1800, 2600, 3400, 4200, 5200, 6400, 7600, 8800, 10000,
        12000, 14000, 17000, 21000, 25000, 32000, 42000, 52000, 64000,
        80000, 100000, 124000, 164000, 228000, 320000, 480000, 640000, 820000,
        1280000
    };

    /// <summary>速度を上げる値段。0段→1段が UpgradeCostBase 個で、1段ごとに UpgradeCostGrowth 倍。仮の値。</summary>
    public const ResourceType UpgradeResource = ResourceType.EE;
    const int UpgradeCostBase = 3;
    const double UpgradeCostGrowth = 1.18;

    const string KeyDeposited = "shapebuster.convert.deposited";
    const string KeyCredit = "shapebuster.convert.credit";
    const string KeyLevel = "shapebuster.convert.speed";
    const float SaveInterval = 2f;

    static bool loaded;
    static bool dirty;
    static BigInteger deposited;
    static long credit;
    static int speedLevel;
    static int unsavedWhole;
    static double bitCarry;
    static float saveWait;

    public static BigInteger Deposited
    {
        get
        {
            EnsureLoaded();
            return deposited;
        }
    }

    public static int SpeedLevel
    {
        get
        {
            EnsureLoaded();
            return speedLevel;
        }
    }

    public static int Speed
    {
        get
        {
            EnsureLoaded();
            int index = Mathf.Clamp(speedLevel, 0, SpeedByLevel.Length - 1);
            return SpeedByLevel[index];
        }
    }

    public static bool TryDeposit(int amount)
    {
        EnsureLoaded();
        if (amount <= 0 || SaveLoader.Inst == null) return false;
        if (SaveLoader.Inst.Get_ResourceCount(ResourceType.AA) < amount) return false;

        SaveLoader.Inst.Request_SaveResource(ResourceType.AA, -amount);
        deposited += amount;
        Save();
        return true;
    }

    public static bool CanUpgradeSpeed()
    {
        EnsureLoaded();
        if (speedLevel >= MaxLevel || deposited > 0) return false;
        return SaveLoader.Inst != null && SaveLoader.Inst.Get_ResourceCount(UpgradeResource) >= UpgradeCost;
    }

    /// <summary>今の段から次の段へ上げる値段（EE）。最大の段なら 0。</summary>
    public static int UpgradeCost
    {
        get
        {
            EnsureLoaded();
            return CostOfLevel(speedLevel);
        }
    }

    /// <summary>level 段から level+1 段へ上げる値段。</summary>
    public static int CostOfLevel(int level)
    {
        if (level < 0 || level >= MaxLevel) return 0;
        return Mathf.Max(1, (int)System.Math.Round(UpgradeCostBase * System.Math.Pow(UpgradeCostGrowth, level)));
    }

    public static bool TryUpgradeSpeed()
    {
        if (!CanUpgradeSpeed() || SaveLoader.Inst == null) return false;
        SaveLoader.Inst.Request_SaveResource(UpgradeResource, -UpgradeCost);
        speedLevel++;
        Save();
        return true;
    }

    public static void DebugSetSpeedLevel(int level)
    {
        EnsureLoaded();
        speedLevel = Mathf.Clamp(level, 0, MaxLevel);
        Save();
    }

    /// <summary>預けているAAと端数を、待たずにDへ換える。</summary>
    public static void DebugFinishDeposit()
    {
        EnsureLoaded();
        if (SaveLoader.Inst == null) return;

        BigInteger total = deposited + credit;
        deposited = BigInteger.Zero;
        credit = 0;
        bitCarry = 0d;

        BigInteger units = total / BitsPerUnit;
        BigInteger rest = total % BitsPerUnit;
        if (rest > long.MaxValue) rest = long.MaxValue;
        credit = (long)rest;
        if (units > 0)
            SaveLoader.Inst.Request_SaveResource(ResourceType.DD, units);
        Save();
    }

    public static void Tick(float deltaTime)
    {
        EnsureLoaded();
        float step = Mathf.Clamp(deltaTime, 0f, 1f);
        if (step <= 0f) return;
        saveWait += step;

        if (deposited > 0 && SaveLoader.Inst != null)
        {
            bitCarry += Speed * (double)step;
            if (bitCarry >= 1d)
            {
                long whole = (long)bitCarry;
                bitCarry -= whole;
                if (deposited < whole)
                {
                    whole = (long)deposited;
                    bitCarry = 0d;
                }

                deposited -= whole;
                credit += whole;
                dirty = true;

                int granted = 0;
                while (credit >= BitsPerUnit)
                {
                    credit -= BitsPerUnit;
                    granted++;
                }

                if (granted > 0)
                    unsavedWhole += granted;
                if (deposited == 0)
                    bitCarry = 0d;
            }
        }

        if (dirty && (deposited == 0 || saveWait >= SaveInterval))
            Flush();
    }

    /// <summary>まだディスクに書いていない整数D。購入判定はこれに足す。</summary>
    public static int UnsavedWhole
    {
        get
        {
            EnsureLoaded();
            return unsavedWhole;
        }
    }

    /// <summary>溜まった整数Dと変換の途中経過を、まとめて一度だけ書く。</summary>
    public static void Flush()
    {
        EnsureLoaded();
        if (unsavedWhole > 0 && SaveLoader.Inst != null)
        {
            int grant = unsavedWhole;
            unsavedWhole = 0;
            SaveLoader.Inst.Request_SaveResource(ResourceType.DD, grant);
        }

        if (dirty)
            Save();
        dirty = false;
        saveWait = 0f;
    }

    public static string RateText()
    {
        return (1m / BitsPerUnit).ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>所持Dに、まだ書いていない整数と1未満の端数を足した表示。</summary>
    public static string OwnedText()
    {
        EnsureLoaded();
        BigInteger whole = SaveLoader.Inst != null ? SaveLoader.Inst.Get_ResourceCount(ResourceType.DD) : BigInteger.Zero;
        if (whole < 0) whole = BigInteger.Zero;
        whole += unsavedWhole;
        decimal pending = (decimal)credit / BitsPerUnit;
        return FormatDecimal(ToDecimal(whole) + pending);
    }

    public static string FormatUnits(BigInteger units)
    {
        if (units < 0) units = BigInteger.Zero;
        return FormatDecimal(ToDecimal(units));
    }

    static string FormatDecimal(decimal value)
    {
        if (value < 0m) value = 0m;
        if (value == 0m) return "0.00000";
        if (value < 1m) return value.ToString("0.00000", CultureInfo.InvariantCulture);
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    static decimal ToDecimal(BigInteger value)
    {
        if (value > (BigInteger)decimal.MaxValue) return decimal.MaxValue;
        return (decimal)value;
    }

    public static string TimeText()
    {
        EnsureLoaded();
        if (deposited <= 0 || Speed <= 0) return "00:00";

        BigInteger seconds = deposited / Speed;
        if (seconds > int.MaxValue) seconds = int.MaxValue;
        int total = (int)seconds;
        int hour = total / 3600;
        int minute = (total % 3600) / 60;
        int second = total % 60;
        if (hour > 0)
            return $"{hour}:{minute:00}:{second:00}";
        return $"{minute:00}:{second:00}";
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        deposited = ReadBig(KeyDeposited);
        credit = (long)ReadBig(KeyCredit);
        if (credit < 0) credit = 0;
        speedLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, 0), 0, MaxLevel);
    }

    static BigInteger ReadBig(string key)
    {
        string text = PlayerPrefs.GetString(key, "0");
        if (BigInteger.TryParse(text, out var value) && value > 0)
            return value;
        return BigInteger.Zero;
    }

    /// <summary>変換のセーブ（PlayerPrefs）を消す。データを全部消すときに SaveLoader から呼ぶ。</summary>
    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(KeyDeposited);
        PlayerPrefs.DeleteKey(KeyCredit);
        PlayerPrefs.DeleteKey(KeyLevel);
        PlayerPrefs.Save();
    }

    static void Save()
    {
        if (SaveLoader.SaveBlocked) return;
        PlayerPrefs.SetString(KeyDeposited, deposited.ToString());
        PlayerPrefs.SetString(KeyCredit, credit.ToString());
        PlayerPrefs.SetInt(KeyLevel, speedLevel);
        PlayerPrefs.Save();
    }
}
