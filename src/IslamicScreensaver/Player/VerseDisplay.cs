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

    /// <summary>The panel while the isti'ādhah or the basmalah is being recited, before the surah itself.</summary>
    public static VerseDisplay Prelude(bool istiadhah, string surahNameArabic, string surahNameSimple, int versesCount, string reciterName, bool showTranslation) =>
        new(surahNameArabic, surahNameSimple, 0, versesCount,
            istiadhah ? IstiadhahText : BismillahText,
            [],
            showTranslation ? (istiadhah ? IstiadhahTranslation : BismillahTranslation) : null,
            reciterName,
            IsBismillah: true);
}
