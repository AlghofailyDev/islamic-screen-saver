using System.Net.Http;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Settings;

/// <summary>
/// Everything the settings pages share: the live <see cref="AppSettings"/>, the content cache and
/// downloader, and the catalogs (reciters, chapters, translations). Saves are debounced so a slider
/// drag does not write the file sixty times a second.
/// </summary>
internal sealed class SettingsContext
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _saveTimer;
    private bool _dirty;

    public SettingsContext()
    {
        Settings = SettingsStore.Load();
        Cache = ContentCache.Default();
        Api = new QuranApiClient(new HttpClient());
        Downloads = new DownloadManager(new ContentDownloader(Api, new HttpClient(), Cache), Cache);

        Reciters = Cache.ReadReciters() ?? [];
        Chapters = Cache.ReadChapters() ?? [];
        Translations = Cache.ReadTranslations() ?? [];

        _saveTimer = new DispatcherTimer { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) => Flush();
    }

    public AppSettings Settings { get; }
    public ContentCache Cache { get; }
    public IQuranApi Api { get; }
    public DownloadManager Downloads { get; }
    public SamplePlayer Samples { get; } = new();

    public IReadOnlyList<Reciter> Reciters { get; private set; }
    public IReadOnlyList<Chapter> Chapters { get; private set; }
    public IReadOnlyList<TranslationResource> Translations { get; private set; }

    public bool HasCatalog => Reciters.Count > 0 && Chapters.Count > 0 && Translations.Count > 0;

    /// <summary>Raised after the catalogs are (re)loaded from the API.</summary>
    public event Action? CatalogChanged;

    /// <summary>Raised whenever a page changes a setting, so other pages (and previews) can react.</summary>
    public event Action? SettingsChanged;

    /// <summary>Set by the window: push a detail page over the current one.</summary>
    public Action<System.Windows.Controls.UserControl>? NavigateTo { get; set; }

    /// <summary>Set by the window: return to the page beneath the current detail page.</summary>
    public Action? NavigateBack { get; set; }

    /// <summary>Call after mutating <see cref="Settings"/>. Persists shortly afterwards.</summary>
    public void Changed()
    {
        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        SettingsChanged?.Invoke();
    }

    public void Flush()
    {
        _saveTimer.Stop();
        if (!_dirty)
            return;
        _dirty = false;
        SettingsStore.Save(Settings);
    }

    public async Task<bool> RefreshCatalogAsync()
    {
        try
        {
            var reciters = await Api.GetRecitersAsync();
            var chapters = await Api.GetChaptersAsync();
            var translations = await Api.GetTranslationsAsync();
            await Cache.WriteRecitersAsync(reciters);
            await Cache.WriteChaptersAsync(chapters);
            await Cache.WriteTranslationsAsync(translations);
            Reciters = reciters;
            Chapters = chapters;
            Translations = translations;
            CatalogChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not refresh the catalog.", ex);
            return false;
        }
    }

    /// <summary>The surahs playback is limited to (all 114 when nothing is selected).</summary>
    public IReadOnlyList<int> EffectiveSurahs => Settings.SelectedSurahs ?? Core.Playlist.PlaylistBuilder.AllSurahs;

    public string ReciterName(int id) => Reciters.FirstOrDefault(r => r.Id == id)?.DisplayName ?? $"Reciter {id}";

    public Chapter? Chapter(int number) => Chapters.FirstOrDefault(c => c.Number == number);
}
