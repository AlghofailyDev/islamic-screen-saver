using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Core.Timing;

/// <summary>
/// Maps a playback position to the verse (and word) being recited. The screensaver polls this
/// dozens of times a second, so lookups are binary searches over timings sorted by start time.
/// </summary>
public sealed class VerseClock
{
    /// <summary>Verse index -1 = before the first verse (the reciter's bismillah, or leading silence). Word 0 = no word yet.</summary>
    public readonly record struct Position(int VerseIndex, int WordPosition)
    {
        public static Position BeforeFirst => new(-1, 0);
    }

    private readonly IReadOnlyList<VerseTiming> _timings;
    private readonly int[] _starts;

    public VerseClock(IReadOnlyList<VerseTiming> timings)
    {
        _timings = timings.OrderBy(t => StartOf(t)).ToList();
        _starts = _timings.Select(StartOf).ToArray();
    }

    /// <summary>
    /// A verse is on screen from the moment its first word is heard until the next verse's first word.
    /// The verse-level timestamp and the first word's can differ by a few tens of milliseconds; the earlier wins.
    /// </summary>
    private static int StartOf(VerseTiming t) =>
        t.Words.Count > 0 ? Math.Min(t.FromMs, t.Words[0].FromMs) : t.FromMs;

    public int VerseCount => _timings.Count;

    public VerseTiming this[int index] => _timings[index];

    public Position Lookup(int positionMs)
    {
        var verse = LastAtOrBefore(_starts, positionMs);
        if (verse < 0)
            return Position.BeforeFirst;

        var words = _timings[verse].Words;
        var word = 0;
        // Word segments are few (a verse has maybe 5–40 words); a linear scan is cheaper than allocating.
        for (var i = 0; i < words.Count; i++)
        {
            if (words[i].FromMs <= positionMs)
                word = words[i].WordPosition;
            else
                break;
        }
        return new Position(verse, word);
    }

    /// <summary>Playback position at which <paramref name="verseIndex"/> starts, for resuming.</summary>
    public int StartOf(int verseIndex) =>
        verseIndex < 0 || verseIndex >= _starts.Length ? 0 : _starts[verseIndex];

    /// <summary>Where the verse leaves the screen: the next verse's start, or its own end for the last one.</summary>
    public int EndOf(int verseIndex)
    {
        if (verseIndex < 0 || verseIndex >= _starts.Length)
            return _starts.Length > 0 ? _starts[0] : 0;
        return verseIndex + 1 < _starts.Length ? _starts[verseIndex + 1] : Math.Max(_timings[verseIndex].ToMs, _starts[verseIndex]);
    }

    /// <summary>0..1 progress through the verse (or the pre-verse region) at <paramref name="positionMs"/>.</summary>
    public double Progress(int verseIndex, int positionMs)
    {
        var start = verseIndex < 0 ? 0 : StartOf(verseIndex);
        var end = EndOf(verseIndex);
        return end <= start ? 0 : Math.Clamp((double)(positionMs - start) / (end - start), 0, 1);
    }

    private static int LastAtOrBefore(int[] sortedStarts, int value)
    {
        var lo = 0;
        var hi = sortedStarts.Length - 1;
        var result = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (sortedStarts[mid] <= value)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return result;
    }
}
