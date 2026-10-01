namespace IslamicScreensaver.Core.Content;

/// <summary>
/// The thirty ajzāʾ, and which one a verse falls in.
///
/// The boundaries are the Madani muṣḥaf's, as published by Quran.com (<c>api/v4/juzs</c>, checked
/// 2026-10-01) — the same muṣḥaf whose text and ۞ marks the panel displays, so the juzʾ shown always
/// changes exactly where the verse on screen says it does.
///
/// Parts are shown by number only. An earlier version named them, from memory rather than any
/// source, and mixed two traditions: the names South Asian muṣḥafs give their paras, against the
/// Madani boundaries. Those muṣḥafs divide seven of the parts at different verses, so seven labels
/// disagreed with the screen — two of them naming a verse from the previous juzʾ — and one boundary
/// (juzʾ 4, at 3:92 rather than 3:93) was taken from that tradition too. A number has no tradition.
/// </summary>
public static class Juz
{
    public const int Count = 30;

    /// <summary>First verse of each juzʾ, in order.</summary>
    private static readonly VerseKey[] Starts =
    [
        new(1, 1),    new(2, 142),  new(2, 253),  new(3, 93),   new(4, 24),
        new(4, 148),  new(5, 82),   new(6, 111),  new(7, 88),   new(8, 41),
        new(9, 93),   new(11, 6),   new(12, 53),  new(15, 1),   new(17, 1),
        new(18, 75),  new(21, 1),   new(23, 1),   new(25, 21),  new(27, 56),
        new(29, 46),  new(33, 31),  new(36, 28),  new(39, 32),  new(41, 47),
        new(46, 1),   new(51, 31),  new(58, 1),   new(67, 1),   new(78, 1),
    ];

    /// <summary>The juzʾ (1–30) a verse belongs to. Out-of-range input is clamped rather than thrown over.</summary>
    public static int Of(VerseKey verse)
    {
        var juz = 1;
        for (var i = 1; i < Starts.Length; i++)
        {
            if (verse.CompareTo(Starts[i]) < 0)
                break;
            juz = i + 1;
        }
        return juz;
    }

    public static int Of(int surah, int ayah) =>
        Of(new VerseKey(Math.Clamp(surah, 1, 114), Math.Max(ayah, 1)));

    public static VerseKey StartOf(int juz) => Starts[Math.Clamp(juz, 1, Count) - 1];

    /// <summary>The string-table key for a juzʾ's label: "juz.4" → «الجزء الرابع» / "Juz 4".</summary>
    public static string LabelKey(int juz) => $"juz.{Math.Clamp(juz, 1, Count)}";
}
