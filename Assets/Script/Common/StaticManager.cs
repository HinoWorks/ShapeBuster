using System.Collections.Generic;
using UnityEngine;
using System;
using System.Numerics;
using DG.Tweening;

public enum ResourceType
{

    AA, BB
}

public class ResourceCount
{
    public ResourceType resourceType;
    public int requiredCount;
}


public class StaticManager : MonoBehaviour
{
    // == tag ==
    public static string tag_AroundWall = "AroundWall";
    public static string tag_WorldBase = "WorldBase";



    /// ========= Initial Param
    public static float ingameTime_base = 15f; //インゲーム時間の初期値
    public static int artifactSlotCount = 4; //アーティファクトスロット数
    public static int blockGenerate_initialCount = 10; //セッション開始時のブロック生成初期数
    public static int blockGenerate_count_deltaTime = 3; // ブロック生成時の初期個数
    public static float blockGenerate_duration = 3f; // ブロック生成時間

    // tower
    public static int towerGenerate_height = 3; // タワー高さ
    public static float blockGenerate_towerDuration = 5f; // タワー生成間隔


    // BreakCount 初期値
    public static int breakBonus_createBlockCount = 5; //ブロック破壊ボーナス : ブロック生成数加算
    public static int breakBonus_addTime = 1; // ブロック破壊ボーナス : 時間加算
    public static int breakBonus_changeResource = 2; // ブロック破壊ボーナス : リソース変更数加算




    // 時間調整用
    private const float NormalTimeScale = 1f;
    private static Sequence slowGameTimeSequence;





    private static BigInteger Get_Num(int _count)
    {
        BigInteger num = 1;
        for (int i = 0; i < _count; i++)
        {
            num *= 10;
        }
        return num;
    }


    public static string Get_BigintegerToString(BigInteger _coin)
    {
        if (_coin / Get_Num(3) < 1)
        {
            return (_coin.ToString());
        }
        else if (_coin / Get_Num(6) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(2)) / 10f;
            return (st_coin.ToString("f1") + "K");
        }
        else if (_coin / Get_Num(9) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(5)) / 10f;
            return (st_coin.ToString("f1") + "M");
        }
        else if (_coin / Get_Num(12) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(8)) / 10f;
            return (st_coin.ToString("f1") + "B");
        }
        else if (_coin / Get_Num(15) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(11)) / 10f;
            return (st_coin.ToString("f1") + "T");
        }
        else if (_coin / Get_Num(18) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(14)) / 10f;
            return (st_coin.ToString("f1") + "Q");
        }
        else
        {
            float st_coin = (float)(_coin / Get_Num(17)) / 10f;
            return (st_coin.ToString("f1") + "Qa");
        }
    }

    public static (float num, string unit, int unitInt) Get_BigintegerToUnit(BigInteger _coin)
    {
        if (_coin / Get_Num(3) < 1)
        {
            return (float.Parse(_coin.ToString("f1")), "", 0);
        }
        else if (_coin / Get_Num(6) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(2)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "K", 3);
        }
        else if (_coin / Get_Num(9) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(5)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "M", 6);
        }
        else if (_coin / Get_Num(12) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(8)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "B", 9);
        }
        else if (_coin / Get_Num(15) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(11)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "T", 12);
        }
        else if (_coin / Get_Num(18) < 1)
        {
            float st_coin = (float)(_coin / Get_Num(14)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "q", 15);
        }
        else
        {
            float st_coin = (float)(_coin / Get_Num(17)) / 10f;
            return (float.Parse(st_coin.ToString("f1")), "Qa", 18);
        }

    }


    #region -- Time --
    public static string Get_StringFromDateTime(DateTime _date)
    {
        return _date.ToBinary().ToString();
    }
    public static DateTime Get_DateTimeFromString(string _date)
    {
        return System.DateTime.FromBinary(System.Convert.ToInt64(_date));
    }


    public static void SlowGameTime_PickaxePower()
    {
        return;
        SlowGameTime(0.2f, 0.1f, 0.1f);
    }

    /// <summary>
    /// ゲーム内時間を指定時間だけ遅くし、同じ実時間をかけて徐々に通常速度に戻す。
    /// 減速・復帰の遷移はいずれも returnDuration（未指定時は slowDuration）を使用する。
    /// </summary>
    /// <param name="targetTimeScale">遅くする際の Time.timeScale（0.01～1）</param>
    /// <param name="slowDuration">減速状態を維持する実時間（秒）</param>
    /// <param name="returnDuration">通常速度へ戻す遷移の実時間（秒）。0以下なら slowDuration と同じ</param>
    public static void SlowGameTime(float targetTimeScale, float slowDuration, float returnDuration = -1f)
    {
        if (returnDuration <= 0f) returnDuration = slowDuration;
        targetTimeScale = Mathf.Clamp(targetTimeScale, 0.01f, 1f);
        slowDuration = Mathf.Max(0f, slowDuration);
        returnDuration = Mathf.Max(0f, returnDuration);

        KillSlowGameTimeSequence();

        var seq = DOTween.Sequence().SetUpdate(true);
        var fadeIn = CreateTimeScaleTween(Time.timeScale, targetTimeScale, returnDuration);
        if (fadeIn != null) seq.Append(fadeIn);
        else ApplyTimeScale(targetTimeScale);

        if (slowDuration > 0f) seq.AppendInterval(slowDuration);

        var fadeOut = CreateTimeScaleTween(targetTimeScale, NormalTimeScale, returnDuration);
        if (fadeOut != null) seq.Append(fadeOut);
        else seq.AppendCallback(() => ApplyTimeScale(NormalTimeScale));

        seq.OnComplete(() =>
        {
            ApplyTimeScale(NormalTimeScale);
            slowGameTimeSequence = null;
        });

        slowGameTimeSequence = seq;
        seq.Play();
    }

    /// <summary>ゲーム内時間を即座に通常速度に戻す</summary>
    public static void ResetGameTime()
    {
        KillSlowGameTimeSequence();
        ApplyTimeScale(NormalTimeScale);
    }

    static void KillSlowGameTimeSequence()
    {
        if (slowGameTimeSequence == null) return;
        slowGameTimeSequence.Kill();
        slowGameTimeSequence = null;
    }

    static Tween CreateTimeScaleTween(float from, float to, float duration)
    {
        if (duration <= 0f) return null;
        return DOVirtual.Float(from, to, duration, ApplyTimeScale).SetUpdate(true);
    }

    static void ApplyTimeScale(float scale)
    {
        Time.timeScale = scale;
        DOTween.timeScale = scale;
    }
    #endregion




    #region --- resource check ---
    public static bool IsResourceEnough(ResourceCount[] _requredResources)
    {
        /*
        var isEnough = true;
        foreach (var resource in _requredResources)
        {
            if (resource.requiredCount <= 0) continue;
            if (resource.requiredCount > SaveLoader.Inst.Get_ResourceCount(resource.resourceType))
            {
                isEnough = false;
                break;
            }
        }
        return isEnough;
        */
        return true;
    }
    public static bool IsResourceEnough(ResourceType _resourceType, int _requiredCount)
    {
        //return SaveLoader.Inst.Get_ResourceCount(_resourceType) >= _requiredCount;
        return true;
    }
    #endregion
}
