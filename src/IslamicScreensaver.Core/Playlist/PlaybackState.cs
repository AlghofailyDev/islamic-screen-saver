using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Core.Playlist;

/// <summary>Where playback was when the screensaver last exited, so the next activation carries on.</summary>
public sealed class PlaybackState
{
    /// <summary>Source-qualified, e.g. <c>qurancom:7</c>.</summary>
    public string RecitationId { get; set; } = string.Empty;

    public int Surah { get; set; }

    /// <summary>Zero-based index into the surah's verse timings; -1 means before the first verse.</summary>
    public int VerseIndex { get; set; }

    public DateTimeOffset SavedAt { get; set; }

    public static string DefaultPath { get; } = Path.Combine(AppPaths.LocalDirectory, "playback-state.json");

    public static PlaybackState? Load(string? path = null)
    {
        var file = path ?? DefaultPath;
        return File.Exists(file) ? JsonFileStore.Load<PlaybackState>(file) : null;
    }

    public void Save(string? path = null)
    {
        SavedAt = DateTimeOffset.Now;
        JsonFileStore.Save(path ?? DefaultPath, this);
    }
}
