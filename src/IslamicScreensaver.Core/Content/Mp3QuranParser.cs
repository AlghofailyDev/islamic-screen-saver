using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// JSON → model mapping for mp3quran.net's <c>ayat_timing</c> responses. Verse boundaries only — this
/// source has no word segments, so the word highlight turns itself off for these recitations.
/// Field shapes confirmed against the live API on 2026-09-22.
/// </summary>
public static class Mp3QuranParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Ayah 0 is this source's marker for the basmalah before the surah. It is not a verse, so it is
    /// dropped: the panel shows the prelude text over that stretch the same as it does for any other
    /// recitation, and <see cref="Recitation.AudioIncludesBasmalah"/> is what stops us playing a
    /// second one over it.
    /// </summary>
    public static IReadOnlyList<VerseTiming> ParseTimings(string json, int surah)
    {
        var entries = JsonSerializer.Deserialize<List<AyahTimingDto>>(json, Options)
            ?? throw new FormatException("Empty ayat_timing response.");

        var timings = entries
            .Where(e => e.Ayah is >= 1)
            .Select(e => new VerseTiming(new VerseKey(surah, e.Ayah), e.StartTime, e.EndTime, []))
            .OrderBy(t => t.Key)
            .ToList();

        if (timings.Count == 0)
            throw new FormatException($"ayat_timing returned no verses for surah {surah}.");

        return timings;
    }

    private sealed class AyahTimingDto
    {
        public int Ayah { get; set; }
        [JsonPropertyName("start_time")] public int StartTime { get; set; }
        [JsonPropertyName("end_time")] public int EndTime { get; set; }
    }
}
