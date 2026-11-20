/// <summary>
/// 効果音の種類。SO_SoundData の項目と対応する。
/// 値はアセットに保存されるので、途中に挟まず末尾に足す。
/// </summary>
public enum SEType
{
    None = 0,

    // --- UI ---
    UI_Click = 1,
    UI_Hover = 2,
    UI_Cancel = 3,
    UI_Purchase = 4,
    UI_PurchaseMax = 5,
    UI_PopupOpen = 6,
    UI_Error = 7,

    // --- インゲーム ---
    BallLaunch = 100,
    BlockHit = 101,
    BlockHitCritical = 102,
    BlockBreak = 103,
    ArmorDeflect = 104,
    RegenHeal = 105,
    Explosion = 106,
    Laser = 107,
    Poison = 108,
    ResourceCollect = 109,
    BossSpawn = 110,
    BossDefeat = 111,
    StageClear = 112,
    StageFail = 113,
    /// <summary>ガラスに当たった（割れていないとき）。</summary>
    GlassHit = 114,
    /// <summary>画面切り替えの幕が閉じ始めた。</summary>
    Transition = 115,
}

/// <summary>
/// BGM の場面。曲は場面によらず同じ一覧から流し続け、場面ごとに音量だけを変える。
/// 値はアセットに保存されるので、途中に挟まず末尾に足す。
/// </summary>
public enum BGMType
{
    None = 0,
    Title = 1,
    OutGame = 2,
    InGame = 3,
    Boss = 4,
    Result = 5,
}
