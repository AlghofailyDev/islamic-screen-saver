namespace IslamicScreensaver.CatalogBuilder;

/// <summary>One row of mp3quran.net's riwāyāt table.</summary>
internal sealed record Riwayah(int Id, string Arabic, string English);

/// <summary>
/// Turning a moshaf into a riwāyah and a style.
///
/// mp3quran.net files two entries in its riwāyāt table that are not riwāyāt at all: "المصحف المجود"
/// and "المصحف المعلم" are ways of presenting a recitation, and both are Ḥafṣ ʿan ʿĀṣim. Published
/// as-is they would put Minshawi's mujawwad recording under a different riwāyah from his murattal
/// one and split the same reciter in two, so they are moved to the style where they belong.
/// </summary>
internal static class Riwayat
{
    private const int Hafs = 1;

    /// <summary>The riwāyāt ids that are really styles, and the riwāyah they all belong to.</summary>
    private static readonly HashSet<int> StylesInDisguise = [21, 22];

    public static (string Riwayah, string? Style) Describe(
        IReadOnlyDictionary<int, Riwayah> table, int rewayaId, string? moshafName, bool arabic)
    {
        var style = StyleFrom(moshafName);

        if (!StylesInDisguise.Contains(rewayaId))
            return (Name(table, rewayaId, arabic) ?? style ?? "", style);

        // The label ("المصحف المجود") is the style; the riwāyah underneath is Ḥafṣ.
        return (Name(table, Hafs, arabic) ?? "", Name(table, rewayaId, arabic) ?? style);
    }

    /// <summary>"حفص عن عاصم - مرتل" → "مرتل". A name repeated on both sides carries no style.</summary>
    private static string? StyleFrom(string? moshafName)
    {
        if (string.IsNullOrWhiteSpace(moshafName))
            return null;

        var parts = moshafName.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && !parts[0].Equals(parts[1], StringComparison.Ordinal) ? parts[1] : null;
    }

    private static string? Name(IReadOnlyDictionary<int, Riwayah> table, int id, bool arabic) =>
        table.TryGetValue(id, out var riwayah)
            ? (arabic ? riwayah.Arabic : riwayah.English).Trim()
            : null;
}
