using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Timing;

namespace IslamicScreensaver.Core.Playlist;

/// <summary>Where a reciter's basmalah audio comes from: the first verse of their own Al-Fatihah.</summary>
public sealed record BasmalahClip(string Path, TimeSpan Start, TimeSpan Duration);

/// <summary>
/// Resolves the basmalah for a reciter. Al-Fatihah's first verse *is* بسم الله الرحمن الرحيم, so their
/// own recording supplies it — provided that surah has been downloaded, which is not a given.
/// </summary>
public static class BasmalahSource
{
    /// <summary>The clip, or null when this reciter's Al-Fatihah is missing or carries no timings.</summary>
    public static BasmalahClip? For(ContentCache cache, RecitationId recitation)
    {
        if (!cache.HasSurahAudio(recitation, RecitationProgram.AlFatihah))
            return null;

        var path = cache.AudioPath(recitation, RecitationProgram.AlFatihah);
        if (!File.Exists(path))
            return null;

        var timings = cache.ReadTimings(recitation, RecitationProgram.AlFatihah);
        if (timings is null || timings.Count == 0)
            return null;

        var clock = new VerseClock(timings);
        var start = clock.StartOf(0);
        var end = clock.EndOf(0);
        if (end <= start)
            return null;

        return new BasmalahClip(path, TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end - start));
    }

    public static bool IsAvailable(ContentCache cache, RecitationId recitation) => For(cache, recitation) is not null;

    /// <summary>
    /// Reciters that would play surahs but cannot supply a basmalah, so the settings app can offer to
    /// fetch their Al-Fatihah instead of letting the prelude quietly not happen.
    /// </summary>
    public static IReadOnlyList<RecitationId> RecitationsMissingIt(ContentCache cache, IEnumerable<RecitationId> recitations) =>
        recitations.Distinct().Where(id => !IsAvailable(cache, id)).ToList();
}
