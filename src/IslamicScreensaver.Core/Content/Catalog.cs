using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>How closely the on-screen text can follow a recording.</summary>
public enum TimingLevel
{
    /// <summary>No timings anywhere in this recording: it plays, but the panel cannot follow it.</summary>
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
    [property: JsonConverter(typeof(SurahRangeConverter))] IReadOnlyList<int> AvailableSurahs)
{
    /// <summary>"Rewayat Hafs A'n Assem". Falls back to the Arabic when the source gives only that.</summary>
    public string? RiwayahEnglish { get; init; }

    public string Riwayah(bool arabic) =>
        arabic || string.IsNullOrWhiteSpace(RiwayahEnglish) ? RiwayahArabic : RiwayahEnglish;

    /// <summary>Where the audio lives. <c>{surah}</c> and <c>{surah:000}</c> are replaced per surah.</summary>
    public string? AudioUrlTemplate { get; init; }

    /// <summary>Where the timings live, when they are a plain fetch. Null means "ask the source's API".</summary>
    public string? TimingsUrlTemplate { get; init; }

    /// <summary>
    /// Whether the recording opens a surah with the basmalah — in which case we must not play our own
    /// in front of it. Quran.com's files never do (except Al-Fatihah, where it is verse 1).
    /// mp3quran.net's do, though only some of them label it: the rest leave it as untimed audio before
    /// the first verse, which is why the builder probes rather than assumes. Null means we could not
    /// tell, because the recording has no timings to read it from.
    /// </summary>
    public bool? AudioIncludesBasmalah { get; init; }

    /// <summary>
    /// The surahs of <see cref="AvailableSurahs"/> the panel can follow. Usually all of them, but
    /// mp3quran.net times a recording surah by surah and some sets are only part-way done. Empty when
    /// <see cref="Timing"/> is <see cref="TimingLevel.None"/>.
    /// </summary>
    [JsonConverter(typeof(SurahRangeConverter))]
    public IReadOnlyList<int> TimedSurahs { get; init; } = [];

    public bool HasSurah(int surah) => AvailableSurahs.Contains(surah);

    /// <summary>Whether the verse panel can follow this surah, or must sit still while it plays.</summary>
    public bool IsTimed(int surah) => Timing != TimingLevel.None && TimedSurahs.Contains(surah);

    /// <summary>"حفص عن عاصم · مرتل" — what distinguishes this recitation from the reciter's others.</summary>
    public string Describe(bool arabic)
    {
        var riwayah = Riwayah(arabic);
        var style = arabic ? StyleArabic : StyleEnglish ?? StyleArabic;
        return string.IsNullOrWhiteSpace(style) ? riwayah : $"{riwayah}  ·  {style}";
    }

    public string ResolveUrl(int surah) => Resolve(AudioUrlTemplate, surah);

    public string ResolveTimingsUrl(int surah) => Resolve(TimingsUrlTemplate, surah);

    private static string Resolve(string? template, int surah) =>
        template is null
            ? string.Empty
            : template.Replace("{surah:000}", surah.ToString("000")).Replace("{surah}", surah.ToString());
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
