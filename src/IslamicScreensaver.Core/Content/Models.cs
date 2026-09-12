using System.Globalization;

namespace IslamicScreensaver.Core.Content;

/// <summary>Identifies one ayah as <c>surah:ayah</c> — the join key across text, translations and timings.</summary>
public readonly record struct VerseKey(int Surah, int Ayah) : IComparable<VerseKey>
{
    public static VerseKey Parse(string text)
    {
        if (!TryParse(text, out var key))
            throw new FormatException($"'{text}' is not a verse key of the form surah:ayah.");
        return key;
    }

    public static bool TryParse(string? text, out VerseKey key)
    {
        key = default;
        if (text is null)
            return false;

        var parts = text.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var surah)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ayah)
            || surah is < 1 or > 114 || ayah < 1)
            return false;

        key = new VerseKey(surah, ayah);
        return true;
    }

    public int CompareTo(VerseKey other) =>
        Surah != other.Surah ? Surah.CompareTo(other.Surah) : Ayah.CompareTo(other.Ayah);

    public override string ToString() => $"{Surah}:{Ayah}";
}

public sealed record Reciter(int Id, string Name, string? Style)
{
    public string DisplayName => string.IsNullOrEmpty(Style) ? Name : $"{Name} ({Style})";
}

public sealed record Chapter(
    int Number,
    string NameArabic,
    string NameSimple,
    string NameTranslated,
    int VersesCount,
    bool BismillahPre);

/// <summary>One ayah's Uthmani text, plus its words in order (positions 1..n, end-of-ayah marker excluded).</summary>
public sealed record VerseText(VerseKey Key, string TextUthmani, IReadOnlyList<string> Words);

public sealed record WordSegment(int WordPosition, int FromMs, int ToMs);

public sealed record VerseTiming(VerseKey Key, int FromMs, int ToMs, IReadOnlyList<WordSegment> Words);

/// <summary>Where a surah's recording lives and how its verses line up with the audio.</summary>
public sealed record SurahAudio(int ReciterId, int Surah, string AudioUrl, long? FileSize, IReadOnlyList<VerseTiming> Timings);

public sealed record TranslationResource(int Id, string Name, string AuthorName, string LanguageName);

/// <summary>A surah's translation; <see cref="Texts"/>[i] is ayah i+1, already stripped of footnote markup.</summary>
public sealed record SurahTranslation(int ResourceId, int Surah, IReadOnlyList<string> Texts);
