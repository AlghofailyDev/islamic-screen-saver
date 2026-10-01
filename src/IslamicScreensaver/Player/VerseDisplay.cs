using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Player;

/// <summary>Everything the verse panel needs to draw one moment of playback.</summary>
public sealed record VerseDisplay(
    string SurahNameArabic,
    string SurahNameSimple,
    int VerseNumber,
    int VersesCount,
    string ArabicText,
    IReadOnlyList<string> Words,
    string? Translation,
    string ReciterName,
    bool IsBismillah)
{
    public const string BismillahText = "بِسْمِ ٱللَّهِ ٱلرَّحْمَـٰنِ ٱلرَّحِيمِ";
    public const string BismillahTranslation = "In the name of Allah, the Entirely Merciful, the Especially Merciful.";

    public const string IstiadhahText = "أَعُوذُ بِٱللَّهِ مِنَ ٱلشَّيْطَـٰنِ ٱلرَّجِيمِ";
    public const string IstiadhahTranslation = "I seek refuge in Allah from Satan, the accursed.";

    /// <summary>The surah this moment belongs to, used to work out the juzʾ.</summary>
    public int SurahNumber { get; init; }

    /// <summary>
    /// «الجزء الثلاثون» — where in the muṣḥaf this is. Taken from the verse on screen, so it changes
    /// with the recitation rather than being fixed per surah: al-Baqarah alone spans three ajzāʾ.
    /// </summary>
    public string JuzLabel =>
        SurahNumber < 1 ? string.Empty : Strings.Get(Juz.LabelKey(Juz.Of(SurahNumber, Math.Max(VerseNumber, 1))));

    /// <summary>The panel while the isti'ādhah or the basmalah is being recited, before the surah itself.</summary>
    public static VerseDisplay Prelude(bool istiadhah, int surahNumber, string surahNameArabic, string surahNameSimple, int versesCount, string reciterName, bool showTranslation) =>
        new(surahNameArabic, surahNameSimple, 0, versesCount,
            istiadhah ? IstiadhahText : BismillahText,
            [],
            showTranslation ? (istiadhah ? IstiadhahTranslation : BismillahTranslation) : null,
            reciterName,
            IsBismillah: true)
        { SurahNumber = surahNumber };
}
