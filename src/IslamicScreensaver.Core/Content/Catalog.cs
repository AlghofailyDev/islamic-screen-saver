namespace IslamicScreensaver.Core.Content;

/// <summary>How closely the on-screen text can follow a recording.</summary>
public enum TimingLevel
{
    /// <summary>No timings: the surah plays, but the panel cannot follow it verse by verse.</summary>
    None,

    /// <summary>Verse boundaries only (mp3quran.net).</summary>
    Verse,

    /// <summary>Verse and word boundaries, so the recited word can be lit (quran.com).</summary>
    Word,
}

/// <summary>
/// One recording set: a reciter reciting in a particular riwāyah and style, from one source.
/// This — not the reciter — is what gets downloaded, played and rotated between.
/// </summary>
public sealed record Recitation(
    RecitationId Id,
    string ReciterId,
    string RiwayahArabic,
    string? StyleArabic,
    string? StyleEnglish,
    TimingLevel Timing,
    IReadOnlyList<int> AvailableSurahs)
{
    /// <summary>Where the audio lives. <c>{surah}</c> and <c>{surah:000}</c> are replaced per surah.</summary>
    public string? AudioUrlTemplate { get; init; }

    public bool HasSurah(int surah) => AvailableSurahs.Contains(surah);

    /// <summary>"حفص عن عاصم · مرتل" — what distinguishes this recitation from the reciter's others.</summary>
    public string Describe(bool arabic)
    {
        var style = arabic ? StyleArabic : StyleEnglish ?? StyleArabic;
        return string.IsNullOrWhiteSpace(style) ? RiwayahArabic : $"{RiwayahArabic}  ·  {style}";
    }

    public string ResolveUrl(int surah) =>
        AudioUrlTemplate is null
            ? string.Empty
            : AudioUrlTemplate.Replace("{surah:000}", surah.ToString("000")).Replace("{surah}", surah.ToString());
}

/// <summary>A person, with every recording set we know of for them — across sources.</summary>
public sealed record ReciterEntry(
    string Id,
    string NameArabic,
    string NameEnglish,
    IReadOnlyList<Recitation> Recitations)
{
    public string Name(bool arabic) => arabic && !string.IsNullOrWhiteSpace(NameArabic) ? NameArabic : NameEnglish;

    public bool Matches(string query) =>
        query.Length == 0
        || NameEnglish.Contains(query, StringComparison.OrdinalIgnoreCase)
        || NameArabic.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The catalog we publish: who recites, in what, and which surahs each recording actually has.</summary>
public sealed record Catalog(
    int Version,
    DateTimeOffset BuiltAt,
    IReadOnlyList<ReciterEntry> Reciters)
{
    public const int CurrentVersion = 1;

    public static Catalog Empty { get; } = new(CurrentVersion, DateTimeOffset.MinValue, []);

    public IEnumerable<Recitation> AllRecitations => Reciters.SelectMany(r => r.Recitations);

    public Recitation? Find(RecitationId id) => AllRecitations.FirstOrDefault(r => r.Id == id);

    public ReciterEntry? ReciterOf(RecitationId id) =>
        Reciters.FirstOrDefault(r => r.Recitations.Any(x => x.Id == id));
}
