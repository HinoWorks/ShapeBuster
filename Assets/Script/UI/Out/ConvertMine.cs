using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using UnityEngine;

/// <summary>
/// AAを時間をかけてDへ換える。Dはここ以外では増えない。
/// レートは固定で、Fを1つ払うと速度が1段上がる。変換中は速度を上げられない。
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

    const string KeyDeposited = "shapebuster.convert.deposited";
    const string KeyCredit = "shapebuster.convert.credit";
    const string KeyLevel = "shapebuster.convert.speed";

    static bool loaded;
    static BigInteger deposited;
    static long credit;
    static int speedLevel;
    static double bitCarry;

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
        return SaveLoader.Inst != null && SaveLoader.Inst.Get_ResourceCount(ResourceType.FF) >= 1;
    }

    public static bool TryUpgradeSpeed()
    {
        if (!CanUpgradeSpeed() || SaveLoader.Inst == null) return false;
        SaveLoader.Inst.Request_SaveResource(ResourceType.FF, -1);
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
        if (deposited <= 0 || SaveLoader.Inst == null) return;

        float step = Mathf.Clamp(deltaTime, 0f, 1f);
        if (step <= 0f) return;

        bitCarry += Speed * (double)step;
        if (bitCarry < 1d)
            return;

        long whole = (long)bitCarry;
        bitCarry -= whole;
        if (deposited < whole)
        {
            whole = (long)deposited;
            bitCarry = 0d;
        }

        deposited -= whole;
        credit += whole;

        int granted = 0;
        while (credit >= BitsPerUnit)
        {
            credit -= BitsPerUnit;
            granted++;
        }

        if (deposited == 0)
            bitCarry = 0d;

        if (granted > 0)
            SaveLoader.Inst.Request_SaveResource(ResourceType.DD, granted);

        if (granted > 0 || deposited == 0)
            Save();
    }

    public static string RateText()
    {
        return (1m / BitsPerUnit).ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>次の1Dになる前の溜まり。1Dを所持へ入れたあとは0から。</summary>
    public static string OwnedText()
    {
        EnsureLoaded();
        return FormatDecimal((decimal)credit / BitsPerUnit);
    }

    public static string FormatUnits(BigInteger units)
    {
        if (units < 0) units = BigInteger.Zero;
        return FormatDecimal(ToDecimal(units));
    }

    /// <summary>いま溜まっているD。1に達すると所持へ入り、0から溜め直す。</summary>
    public static string ReturnsText()
    {
        EnsureLoaded();
        return FormatDecimal((decimal)credit / BitsPerUnit);
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

    static void Save()
    {
        PlayerPrefs.SetString(KeyDeposited, deposited.ToString());
        PlayerPrefs.SetString(KeyCredit, credit.ToString());
        PlayerPrefs.SetInt(KeyLevel, speedLevel);
        PlayerPrefs.Save();
    }
}
