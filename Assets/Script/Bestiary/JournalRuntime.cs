using System.Text;
using UnityEngine;

/// <summary>
/// 図鑑の「にっき」（主人公の日記）。段を初めてクリアするごとに 1 ページ読めるようになり、
/// ワクチンを撒いたあとに「そのあと」の 1 ページが増える。ボーナスはない。
/// 読めるページ数はセーブ（クリアした段・ワクチン）から毎回計算する。
/// 「NEW」を消すための「読んだページ数」だけ、図鑑のセーブの id <see cref="Id"/> の count に入れる。
/// 文字は journal.1〜journal.&lt;PageCount&gt;、journal.after。仕様は LevelDesign/Bestiary.md の 10 章。
/// </summary>
public static class JournalRuntime
{
    /// <summary>図鑑のセーブと一覧の中での id。SO_BestiaryData の id と重ならない値にする。</summary>
    public const int Id = 900;
    /// <summary>段ごとのページの数（段 0〜12）。</summary>
    public const int PageCount = 13;
    /// <summary>一覧のカードの絵（Resources からの道すじ）。</summary>
    public const string PortraitPath = "Bestiary/bestiary_journal";

    /// <summary>段のページのうち、読めるページの数。</summary>
    public static int UnlockedPages
    {
        get
        {
            int cleared = SaveLoader.Inst != null ? SaveLoader.Inst.ClearedStageIndex + 1 : 0;
            return Mathf.Clamp(cleared, 0, PageCount);
        }
    }

    /// <summary>「そのあと」のページが読める（ワクチンを撒いた）。</summary>
    public static bool AfterUnlocked => VaccineRuntime.Cleared;

    /// <summary>読めるページの合計（「そのあと」を含む）。</summary>
    public static int UnlockedTotal => UnlockedPages + (AfterUnlocked ? 1 : 0);

    /// <summary>もう開いたページの数。</summary>
    public static int SeenTotal => (int)BestiaryRuntime.Get(Id).count;

    /// <summary>1 ページでも読める。</summary>
    public static bool Found => UnlockedTotal > 0;

    /// <summary>まだ開いていないページがある（図鑑タブとカードの「NEW」）。</summary>
    public static bool HasUnseen => UnlockedTotal > SeenTotal;

    /// <summary>詳細を開いた。今読めるページを全部読んだことにする。</summary>
    public static void MarkSeen()
    {
        BestiaryRuntime.MarkJournalSeen(UnlockedTotal);
    }

    /// <summary>詳細に出す文。読めるページを古い順に並べ、ページごとに見出しを付ける。</summary>
    public static string Text()
    {
        var sb = new StringBuilder();
        int pages = UnlockedPages;
        for (int i = 1; i <= pages; i++)
            Append(sb, Loc.Format("journal.page", i), Loc.Content("journal." + i, ""));
        if (AfterUnlocked)
            Append(sb, Loc.Text("bestiary.afterTitle"), Loc.Content("journal.after", ""));
        if (sb.Length == 0)
            sb.Append(Loc.Text("journal.locked"));
        return sb.ToString();
    }

    static void Append(StringBuilder sb, string title, string body)
    {
        if (string.IsNullOrEmpty(body)) return;
        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append("<b>").Append(title).Append("</b>\n").Append(body);
    }
}
