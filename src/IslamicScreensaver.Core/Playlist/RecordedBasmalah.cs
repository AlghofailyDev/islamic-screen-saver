using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Core.Playlist;

/// <summary>
/// Whether a surah's recording already opens with the basmalah, decided from that surah's own timings.
///
/// This has to be answered per surah, not per recording. mp3quran.net's reciters open most surahs with
/// the basmalah but not all of them, and the same recording can have five seconds of it before an-Nas
/// and none at all before al-Kahf. A per-recording flag gets one of those wrong, and the audible
/// failure — hearing the basmalah twice — is the worse one.
///
/// The signal is simply how much audio comes before the first verse. Quran.com's recordings start
/// verse 1 at zero; mp3quran.net's either label the basmalah "ayah 0" (dropped on parse, leaving the
/// same gap) or do not time it at all. Measured across the recordings that have one, that gap runs
/// from 2.2 to 14.8 seconds, and where there is none it is under a second — so the threshold below
/// sits in an empty stretch rather than on top of real values.
/// </summary>
public static class RecordedBasmalah
{
    /// <summary>
    /// Audio before verse 1 longer than this is the basmalah. A recitation does not open with two
    /// seconds of silence, and the shortest real basmalah measured was 2.2s.
    /// </summary>
    public const int LeadInMs = 1500;

    /// <summary>
    /// True when this recording recites the basmalah before the surah itself, so we must not play
    /// another in front of it.
    /// </summary>
    public static bool PrecedesFirstVerse(IReadOnlyList<VerseTiming>? timings) =>
        timings is { Count: > 0 } && timings[0].FromMs >= LeadInMs;

    /// <summary>
    /// The same question for a cached surah. An untimed recording tells us nothing, so it falls back
    /// to what the source usually does: mp3quran.net's recordings nearly always carry the basmalah,
    /// and every Quran.com recording leaves it out (bar Al-Fatihah, where it is verse 1).
    /// </summary>
    public static bool IsInAudio(ContentCache cache, RecitationId recitation, int surah)
    {
        var timings = cache.ReadTimings(recitation, surah);
        if (timings is { Count: > 0 })
            return PrecedesFirstVerse(timings);

        return recitation.Source == RecitationSource.Mp3Quran;
    }
}
