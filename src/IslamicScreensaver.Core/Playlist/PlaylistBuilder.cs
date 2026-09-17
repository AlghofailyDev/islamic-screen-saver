using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Core.Playlist;

public sealed record PlaylistItem(int ReciterId, int Surah);

/// <summary>
/// Turns settings plus "what is actually downloaded" into the ordered list of surahs to play.
/// Only cached content is eligible — the screensaver never waits on the network.
/// </summary>
public static class PlaylistBuilder
{
    public static IReadOnlyList<int> AllSurahs { get; } = Enumerable.Range(1, 114).ToArray();

    /// <param name="cachedSurahsFor">Surahs whose audio is complete for a reciter (see ContentCache.CachedSurahsFor).</param>
    /// <param name="cachedReciters">Fallback when settings name no reciter: every reciter with anything cached.</param>
    /// <param name="random">Only used for <see cref="PlayOrder.Shuffled"/>; inject a seeded instance in tests.</param>
    public static IReadOnlyList<PlaylistItem> Build(
        AppSettings settings,
        Func<int, IReadOnlyCollection<int>> cachedSurahsFor,
        IReadOnlyCollection<int> cachedReciters,
        Random? random = null)
    {
        var reciters = settings.ReciterIds.Count > 0 ? settings.ReciterIds : cachedReciters.Order().ToList();
        if (reciters.Count == 0)
            return [];

        var available = reciters.ToDictionary(id => id, id => new HashSet<int>(cachedSurahsFor(id)));

        var surahs = (settings.SelectedSurahs ?? AllSurahs)
            .Where(s => available.Values.Any(set => set.Contains(s)))
            .ToList();
        if (surahs.Count == 0)
            return [];

        var rng = random ?? Random.Shared;
        var rotate = settings.RotateReciters && reciters.Count > 1;

        // One pass per rotating reciter, with the turn carried across passes: every surah is heard from
        // each reciter in turn over successive passes, while inside a pass no surah repeats. The engine
        // loops the whole list, so a single selected surah alternates reciters instead of looping one.
        var passes = rotate ? reciters.Count : 1;
        var turn = 0;
        var playlist = new List<PlaylistItem>(surahs.Count * passes);

        for (var pass = 0; pass < passes; pass++)
        {
            if (settings.Order == PlayOrder.Shuffled)
                Shuffle(surahs, rng);

            foreach (var surah in surahs)
            {
                int reciter;
                if (rotate)
                {
                    // Whose turn it is, skipping anyone who does not have this surah, so the rotation
                    // stays fair without ever stalling on a missing download.
                    var picked = -1;
                    for (var i = 0; i < reciters.Count; i++)
                    {
                        var candidate = reciters[(turn + i) % reciters.Count];
                        if (available[candidate].Contains(surah))
                        {
                            picked = candidate;
                            turn = (turn + i + 1) % reciters.Count;
                            break;
                        }
                    }
                    reciter = picked;
                }
                else
                {
                    reciter = reciters.First(id => available[id].Contains(surah));
                }

                playlist.Add(new PlaylistItem(reciter, surah));
            }
        }

        return playlist;
    }

    /// <summary>
    /// Where to begin: the surah that was playing last time, or the top. Shuffled play resumes too —
    /// the order is new each time, but carrying on the surah you were hearing is the point of the setting.
    /// </summary>
    public static int StartIndex(IReadOnlyList<PlaylistItem> playlist, AppSettings settings, PlaybackState? lastState)
    {
        if (playlist.Count == 0 || !settings.ResumeWhereLeftOff || lastState is null)
            return 0;

        // Prefer the exact item (same reciter) so a multi-pass rotation carries on where it stopped;
        // fall back to the first occurrence of the surah if that reciter is no longer in the list.
        for (var i = 0; i < playlist.Count; i++)
        {
            if (playlist[i].Surah == lastState.Surah && playlist[i].ReciterId == lastState.ReciterId)
                return i;
        }
        for (var i = 0; i < playlist.Count; i++)
        {
            if (playlist[i].Surah == lastState.Surah)
                return i;
        }
        return 0;
    }

    private static void Shuffle<T>(IList<T> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
