using System;
using UniRx;
using UnityEngine;

/// <summary>
/// ゾンビワクチンの進み具合。DD を 1 回ぶん（合計の 10%）ずつ注いでゲージを満たし、満タンで完成できる。
/// 完成してエンドロールを見たら cleared を立てる。クリアのあとも遊び続けられる。
/// SaveLoader が起動時に読み込む。注いだときと、クリアしたときに保存する。仕様は LevelDesign/ZombieVaccine.md。
/// </summary>
public static class VaccineRuntime
{
    /// <summary>ゲージを満たすまでに注ぐ回数。1 回でゲージが 10% 進む。</summary>
    public const int PourCount = 10;
    /// <summary>ワクチンの合計の値段（DD）。試しの値。</summary>
    public const long TotalCost = 1000;
    /// <summary>工程の数。注いだ回数 PourCount / StageCount ごと（20%）に 1 工程が終わる。</summary>
    public const int StageCount = 5;

    static int pours;
    static bool cleared;
    static int editVersion;
    static int savedVersion;
    static readonly Subject<Unit> changed = new();

    public static IObservable<Unit> Changed => changed;
    public static int EditVersion => editVersion;
    public static bool IsDirty => editVersion != savedVersion;

    /// <summary>注いだ回数（0〜PourCount）。</summary>
    public static int Pours => pours;
    /// <summary>ゲージの割合（0〜1）。</summary>
    public static float Progress => (float)pours / PourCount;
    /// <summary>ゲージが満タン。</summary>
    public static bool IsFull => pours >= PourCount;
    /// <summary>ワクチンを撒いて、エンドロールまで見た。</summary>
    public static bool Cleared => cleared;
    /// <summary>1 回に注ぐ DD。</summary>
    public static long PourCost => TotalCost / PourCount;

    /// <summary>終わった工程の数（0〜StageCount）。最後の工程が終わると満タン。</summary>
    public static int StagesDone
    {
        get
        {
            int perStage = Mathf.Max(1, PourCount / StageCount);
            return Mathf.Clamp(pours / perStage, 0, StageCount);
        }
    }

    /// <summary>ワクチンのタブを出すか（スキルツリーの「ワクチン研究所」）。</summary>
    public static bool Unlocked => SkillTreeBonus.VaccineLabUnlocked;

    public static bool CanPour()
    {
        if (!Unlocked || IsFull) return false;
        return StaticManager.IsResourceEnough(ResourceType.DD, (int)PourCost);
    }

    /// <summary>DD を 1 回ぶん払って、ゲージを進める。</summary>
    public static bool TryPour()
    {
        ConvertMine.Flush();
        if (!CanPour() || SaveLoader.Inst == null) return false;

        SaveLoader.Inst.Request_SaveResource(ResourceType.DD, -PourCost);
        pours++;
        Touch();
        return true;
    }

    /// <summary>エンドロールを見終えた。</summary>
    public static void MarkCleared()
    {
        if (cleared) return;
        cleared = true;
        Touch();
    }

    static void Touch()
    {
        editVersion++;
        changed.OnNext(Unit.Default);
        Flush();
    }

    #region -- 保存 --
    public static void Flush()
    {
        if (SaveLoader.Inst != null)
            SaveLoader.Inst.Request_SaveVaccine();
    }

    public static void Replace(VaccineSave save)
    {
        pours = save != null ? Mathf.Clamp(save.pours, 0, PourCount) : 0;
        cleared = save != null && save.cleared;
        editVersion = 0;
        savedVersion = 0;
    }

    public static VaccineSave Capture()
    {
        return new VaccineSave { pours = pours, cleared = cleared };
    }

    public static void MarkSaved(int version)
    {
        savedVersion = version;
    }
    #endregion

#if UNITY_EDITOR
    /// <summary>試すとき用。進み具合を直接決める。</summary>
    public static void DebugSet(int newPours, bool newCleared)
    {
        pours = Mathf.Clamp(newPours, 0, PourCount);
        cleared = newCleared;
        Touch();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        pours = 0;
        cleared = false;
        editVersion = 0;
        savedVersion = 0;
    }
}

/// <summary>ワクチンのセーブ。Easy Save 3 で書く。</summary>
[Serializable]
public class VaccineSave
{
    public int pours;
    public bool cleared;
}
