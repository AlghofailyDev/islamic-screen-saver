using System.Net.Http;
using System.Runtime.InteropServices;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Seeding;

/// <summary>
/// <c>/seed</c>: fetch the starter content into the cache with no UI. A GUI app has no console, so we
/// attach to the parent's (the install script's) to print progress there; if there is none, the log
/// still records the outcome.
/// </summary>
internal static class StarterSeeder
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    public static async Task<int> RunAsync()
    {
        var console = AttachConsole(AttachParentProcess);
        void Say(string line)
        {
            Log.Info(line);
            if (console)
                Console.WriteLine(line);
        }

        try
        {
            var cache = ContentCache.For(Core.Settings.SettingsStore.Load());
            var downloader = new ContentDownloader(new QuranApiClient(new HttpClient()), new HttpClient(), cache);

            Say($"Fetching starter content into {cache.RootDirectory}");
            Say($"  reciter {StarterContent.ReciterId}, {StarterContent.Surahs.Count} surahs, translation {StarterContent.TranslationId}");

            string? lastItem = null;
            var progress = new Progress<DownloadProgress>(p =>
            {
                if (p.Item == lastItem)
                    return; // One line per item is plenty for a script; the log stays readable.
                lastItem = p.Item;
                Say($"  {p.Item}{(p.TotalBytes is { } t ? $" ({t / 1024} KB)" : "")}");
            });

            try
            {
                await StarterContent.DownloadAsync(downloader, progress);
            }
            catch (ContentUnavailableException ex)
            {
                Say($"  skipped: {ex.Message}");
            }

            Say($"Done. {cache.CachedSurahsFor(StarterContent.ReciterId).Count} surahs ready for reciter {StarterContent.ReciterId}.");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Seeding failed.", ex);
            Say($"Seeding failed: {ex.Message}");
            return 1;
        }
    }
}
