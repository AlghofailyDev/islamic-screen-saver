namespace IslamicScreensaver.Core.Content;

/// <summary>
/// The thirty ajzāʾ, and which one a verse falls in.
///
/// Each juzʾ is known by its opening words rather than only its number — جزء عمّ, جزء تبارك — which
/// is how a reader placed the recitation long before anyone numbered the parts. The names here are
/// the traditional kunyas as printed in a muṣḥaf, so a few of them quote a verse just after the
/// boundary (juzʾ 7 is known as «وإذا سمعوا», 5:83, though the part begins at 5:82).
/// </summary>
public static class Juz
{
    public const int Count = 30;

    /// <summary>First verse of each juzʾ, in order. Ḥafṣ boundaries.</summary>
    private static readonly VerseKey[] Starts =
    [
        new(1, 1),    new(2, 142),  new(2, 253),  new(3, 92),   new(4, 24),
        new(4, 148),  new(5, 82),   new(6, 111),  new(7, 88),   new(8, 41),
        new(9, 93),   new(11, 6),   new(12, 53),  new(15, 1),   new(17, 1),
        new(18, 75),  new(21, 1),   new(23, 1),   new(25, 21),  new(27, 56),
        new(29, 46),  new(33, 31),  new(36, 28),  new(39, 32),  new(41, 47),
        new(46, 1),   new(51, 31),  new(58, 1),   new(67, 1),   new(78, 1),
    ];

    private static readonly (string Arabic, string English)[] Names =
    [
        ("آلم", "Alif Lam Mim"),          ("سيقول", "Sayaqul"),
        ("تلك الرسل", "Tilka r-Rusul"),   ("لن تنالوا", "Lan Tanalu"),
        ("والمحصنات", "Wal-Muhsanat"),    ("لا يحب الله", "La Yuhibbu Llah"),
        ("وإذا سمعوا", "Wa Idha Sami'u"), ("ولو أننا", "Wa Law Annana"),
        ("قال الملأ", "Qala l-Mala"),      ("واعلموا", "Wa'lamu"),
        ("يعتذرون", "Ya'tadhirun"),        ("وما من دابة", "Wa Ma Min Dabbah"),
        ("وما أبرئ", "Wa Ma Ubarri'u"),   ("ربما", "Rubama"),
        ("سبحان الذي", "Subhana lladhi"), ("قال ألم", "Qala Alam"),
        ("اقترب", "Iqtaraba"),             ("قد أفلح", "Qad Aflaha"),
        ("وقال الذين", "Wa Qala lladhina"), ("أمّن خلق", "Amman Khalaqa"),
        ("اتل ما أوحي", "Utlu Ma Uhiya"),  ("ومن يقنت", "Wa Man Yaqnut"),
        ("وما لي", "Wa Ma Liya"),          ("فمن أظلم", "Fa Man Azlamu"),
        ("إليه يرد", "Ilayhi Yuraddu"),    ("حم", "Ha Mim"),
        ("قال فما خطبكم", "Qala Fama Khatbukum"), ("قد سمع الله", "Qad Sami'a Llah"),
        ("تبارك", "Tabaraka"),             ("عمَّ", "'Amma"),
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

    /// <summary>"عمَّ" / "'Amma" — the name alone, without the word "juzʾ" in front of it.</summary>
    public static string Name(int juz, bool arabic)
    {
        var index = Math.Clamp(juz, 1, Count) - 1;
        return arabic ? Names[index].Arabic : Names[index].English;
    }

    public static VerseKey StartOf(int juz) => Starts[Math.Clamp(juz, 1, Count) - 1];
}
