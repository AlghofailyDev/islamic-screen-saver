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
}
