namespace IslamicScreensaver.Core.Playlist;

public enum RecitationPart
{
    /// <summary>أعوذ بالله من الشيطان الرجيم — once, before recitation begins.</summary>
    Istiadhah,

    /// <summary>بسم الله الرحمن الرحيم — before a surah that does not open with it.</summary>
    Basmalah,

    /// <summary>The surah recording itself.</summary>
    Surah,
}

/// <summary>One thing to play, in order. <see cref="Item"/> names the surah a part belongs to.</summary>
public sealed record RecitationStep(RecitationPart Part, PlaylistItem Item, int PlaylistIndex);

/// <summary>
/// Turns a playlist into the sequence actually recited, following the etiquette of recitation:
/// the isti'ādhah once at the start, and the basmalah before each surah that needs one. This is how
/// the Qur'an is recited, not a preference, so there is nothing here to switch off.
/// </summary>
/// <remarks>
/// Quran.com's surah recordings are uniform: only Al-Fatihah contains the basmalah (as its first
/// verse). At-Tawbah is recited without one. Every other surah therefore needs the basmalah supplied.
/// mp3quran.net's recordings mostly open with one already — and not uniformly, but surah by surah.
/// <c>audioHasBasmalah</c> is how the caller says so; see <see cref="RecordedBasmalah"/>. A second
/// basmalah over the first would be worse than none.
/// </remarks>
public static class RecitationProgram
{
    public const int AlFatihah = 1;
    public const int AtTawbah = 9;

    /// <summary>Whether we have to supply the basmalah for this surah, given what the recording holds.</summary>
    public static bool NeedsBasmalah(int surah, bool audioHasBasmalah = false) =>
        !audioHasBasmalah && surah is not (AlFatihah or AtTawbah);

    /// <param name="startIndex">Where in the playlist to begin.</param>
    /// <param name="startsMidSurah">True when resuming part-way into the first surah: it has already begun, so no basmalah for it.</param>
    /// <param name="audioHasBasmalah">
    /// Answers, per playlist item, whether that recording already opens with the basmalah. The default
    /// says none of them do, which is true of every Quran.com recording.
    /// </param>
    public static IReadOnlyList<RecitationStep> Build(
        IReadOnlyList<PlaylistItem> playlist,
        int startIndex,
        bool startsMidSurah,
        Func<PlaylistItem, bool>? audioHasBasmalah = null)
    {
        if (playlist.Count == 0)
            return [];

        startIndex = Math.Clamp(startIndex, 0, playlist.Count - 1);
        var steps = new List<RecitationStep>(playlist.Count * 2 + 1)
        {
            new(RecitationPart.Istiadhah, playlist[startIndex], startIndex),
        };

        for (var i = 0; i < playlist.Count; i++)
        {
            var index = (startIndex + i) % playlist.Count;
            var item = playlist[index];

            // The surah we resume into is already under way, so its basmalah has passed. The
            // recording is only consulted for surahs that would otherwise need one, which keeps the
            // predicate — a timings file read, per item — off Al-Fatihah and at-Tawbah.
            var isResumedSurah = i == 0 && startsMidSurah;
            if (!isResumedSurah && NeedsBasmalah(item.Surah) && !(audioHasBasmalah?.Invoke(item) ?? false))
                steps.Add(new RecitationStep(RecitationPart.Basmalah, item, index));

            steps.Add(new RecitationStep(RecitationPart.Surah, item, index));
        }

        // The first surah of a resumed session is already under way, so its basmalah was left out
        // above. The program loops, though, and every lap after the first starts that surah from its
        // beginning — so the basmalah is put back at the very end, where it lands immediately before
        // the loop returns to it. Without this the surah we resumed into opens bare on every lap.
        var resumed = playlist[startIndex];
        if (startsMidSurah && NeedsBasmalah(resumed.Surah) && !(audioHasBasmalah?.Invoke(resumed) ?? false))
            steps.Add(new RecitationStep(RecitationPart.Basmalah, resumed, startIndex));

        return steps;
    }

    /// <summary>
    /// Where playback returns to when the program ends. The isti'ādhah opens a session and is not
    /// repeated; a second lap through the surahs starts at the first basmalah — or, after a resumed
    /// first pass, at the surah whose basmalah <see cref="Build"/> appended to the end.
    /// </summary>
    public static int LoopStart(IReadOnlyList<RecitationStep> steps) =>
        steps.Count > 1 && steps[0].Part == RecitationPart.Istiadhah ? 1 : 0;
}
