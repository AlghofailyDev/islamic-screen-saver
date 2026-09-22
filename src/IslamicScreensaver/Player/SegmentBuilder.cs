using System.IO;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Core.Timing;

namespace IslamicScreensaver.Player;

/// <summary>Turns a recitation program into playable segments, resolving where each part's audio comes from.</summary>
internal static class SegmentBuilder
{
    private static string BundledPath(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "audio", name);

    /// <summary>The recording shipped with the app, used when the user has not chosen their own.</summary>
    public static string BundledIstiadhahPath { get; } = BundledPath("istiadhah.mp3");

    /// <summary>A basmalah for reciters whose own Al-Fatihah is not downloaded.</summary>
    public static string BundledBasmalahPath { get; } = BundledPath("basmalah.mp3");

    public static string? ResolveIstiadhah() => Resolve(BundledIstiadhahPath, "Isti'adhah");

    /// <summary>The fallback basmalah recording — only used when a reciter cannot supply their own.</summary>
    public static string? ResolveBasmalahFallback() => Resolve(BundledBasmalahPath, "Basmalah");

    private static string? Resolve(string path, string what)
    {
        if (File.Exists(path))
            return path;

        Log.Error($"{what} recording is missing from {path}; that part will be skipped.");
        return null;
    }

    public static IReadOnlyList<AudioSegment> Build(IReadOnlyList<RecitationStep> steps, ContentCache cache)
    {
        var istiadhah = ResolveIstiadhah();
        var basmalahFallback = ResolveBasmalahFallback();
        var basmalahByReciter = new Dictionary<RecitationId, BasmalahClip?>();
        var segments = new List<AudioSegment>(steps.Count);
        var missingBasmalah = new HashSet<RecitationId>();

        // Audio from outside Quran.com is mastered differently, so match its level to the recitation.
        var istiadhahGain = istiadhah is null ? 1f : LevelAgainstRecitation(istiadhah, steps, cache);
        float? basmalahGain = null;

        foreach (var step in steps)
        {
            switch (step.Part)
            {
                case RecitationPart.Istiadhah when istiadhah is not null:
                    segments.Add(new AudioSegment(step.Part, istiadhah, TimeSpan.Zero, null, step.Item, step.PlaylistIndex) { Gain = istiadhahGain });
                    break;

                case RecitationPart.Basmalah:
                    if (!basmalahByReciter.TryGetValue(step.Item.Recitation, out var basmalah))
                        basmalahByReciter[step.Item.Recitation] = basmalah = BasmalahSource.For(cache, step.Item.Recitation);

                    if (basmalah is { } b)
                    {
                        // The reciter's own voice, sliced from their Al-Fatihah: same master, no gain needed.
                        segments.Add(new AudioSegment(step.Part, b.Path, b.Start, b.Duration, step.Item, step.PlaylistIndex));
                    }
                    else if (basmalahFallback is not null)
                    {
                        missingBasmalah.Add(step.Item.Recitation);
                        segments.Add(new AudioSegment(step.Part, basmalahFallback, TimeSpan.Zero, null, step.Item, step.PlaylistIndex)
                        {
                            Gain = FallbackGain(basmalahFallback, step.Item, cache, ref basmalahGain),
                        });
                    }
                    else
                    {
                        missingBasmalah.Add(step.Item.Recitation);
                    }
                    break;

                case RecitationPart.Surah:
                    var path = cache.AudioPath(step.Item.Recitation, step.Item.Surah);
                    if (File.Exists(path))
                        segments.Add(new AudioSegment(step.Part, path, TimeSpan.Zero, null, step.Item, step.PlaylistIndex));
                    else
                        Log.Error($"Audio missing for {step.Item.Recitation}/{step.Item.Surah} despite manifest; skipping.");
                    break;
            }
        }

        if (missingBasmalah.Count > 0)
            Log.Info($"Reciter(s) {string.Join(", ", missingBasmalah.Select(r => r.ToString()).Order())} have no Al-Fatihah downloaded, so the {(basmalahFallback is null ? "basmalah is skipped for them" : "bundled basmalah is used instead of their own voice")}.");

        return segments;
    }

    /// <summary>Matches an outside recording to the first surah it will introduce, past that surah's quiet opening.</summary>
    private static float LevelAgainstRecitation(string path, IReadOnlyList<RecitationStep> steps, ContentCache cache)
    {
        var firstSurah = steps.FirstOrDefault(s => s.Part == RecitationPart.Surah);
        if (firstSurah is null)
            return 1f;

        var reference = cache.AudioPath(firstSurah.Item.Recitation, firstSurah.Item.Surah);
        return File.Exists(reference)
            ? Loudness.MatchGain(path, reference, TimeSpan.FromSeconds(5))
            : 1f;
    }

    /// <summary>Measured once and reused: every fallback basmalah is the same file against the same recitation.</summary>
    private static float FallbackGain(string path, PlaylistItem item, ContentCache cache, ref float? cached)
    {
        if (cached is { } value)
            return value;

        var reference = cache.AudioPath(item.Recitation, item.Surah);
        var gain = File.Exists(reference) ? Loudness.MatchGain(path, reference, TimeSpan.FromSeconds(5)) : 1f;
        cached = gain;
        return gain;
    }
}
