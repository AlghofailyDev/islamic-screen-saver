namespace IslamicScreensaver.Core.Content;

/// <summary>
/// What a fresh install fetches so the screensaver has something to play: one reciter, Al-Fatihah and
/// Juz ʿAmma, one English translation. Small enough to finish in a minute on a normal connection.
/// </summary>
public static class StarterContent
{
    /// <summary>Mishari Rashid al-ʿAfasy, murattal — Quran.com recitation id 7.</summary>
    public static RecitationId Recitation { get; } = RecitationId.QuranCom(7);

    /// <summary>Saheeh International — Quran.com translation resource id 20.</summary>
    public const int TranslationId = 20;

    public static IReadOnlyList<int> Surahs { get; } = [1, .. Enumerable.Range(78, 114 - 78 + 1)];

    public static async Task DownloadAsync(ContentDownloader downloader, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new DownloadProgress("catalog", 0, null));
        await downloader.RefreshCatalogAsync(ct);

        foreach (var surah in Surahs)
        {
            ct.ThrowIfCancellationRequested();
            await downloader.EnsureSurahTextAsync(surah, ct);
            await downloader.EnsureSurahTranslationAsync(TranslationId, surah, ct);
            try
            {
                await downloader.EnsureSurahAudioAsync(Recitation, surah, progress, ct);
            }
            catch (ContentUnavailableException ex)
            {
                // One missing recording must not stop the whole starter set.
                Log.Info(ex.Message);
            }
        }
    }
}
