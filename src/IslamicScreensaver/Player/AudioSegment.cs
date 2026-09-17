using IslamicScreensaver.Core.Playlist;

namespace IslamicScreensaver.Player;

/// <summary>
/// One stretch of audio to play: a whole surah recording, or a slice of one. The basmalah is a slice
/// of the reciter's own Al-Fatihah (its first verse), so no extra download or foreign voice is needed.
/// </summary>
internal sealed record AudioSegment(
    RecitationPart Part,
    string Path,
    TimeSpan Start,
    TimeSpan? Duration,
    PlaylistItem Item,
    int PlaylistIndex)
{
    public bool IsSurah => Part == RecitationPart.Surah;

    /// <summary>
    /// Level correction for this segment. Audio from outside Quran.com (the isti'ādhah) is mastered
    /// differently, so it is matched to the recitation rather than jumping in volume.
    /// </summary>
    public float Gain { get; init; } = 1f;
}
