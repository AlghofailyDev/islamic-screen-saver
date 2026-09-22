namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Which copy of the catalog the app uses.
///
/// One ships next to the binary, so a fresh install knows every reciter before it has spoken to
/// anything. A newer one may later be downloaded into the cache; whichever was built most recently
/// wins, which also means a bad download can be undone by deleting one file.
///
/// The screensaver reads this the same way the settings app does — it is a local file, so the rule
/// that <c>/s</c> never touches the network still holds.
/// </summary>
public sealed class CatalogProvider(string cacheRoot, string? appDirectory = null)
{
    /// <summary>The copy published with the build.</summary>
    public string ShippedPath { get; } =
        Path.Combine(appDirectory ?? AppContext.BaseDirectory, "assets", "catalog", CatalogStore.FileName);

    /// <summary>The copy downloaded into the user's cache, if there is one.</summary>
    public string DownloadedPath { get; } = Path.Combine(cacheRoot, "catalog", CatalogStore.FileName);

    /// <summary>
    /// The better of the two, or <see cref="Catalog.Empty"/> when neither can be read. Never throws:
    /// a missing catalog degrades the reciter list, it does not stop the app.
    /// </summary>
    public Catalog Load()
    {
        var shipped = CatalogStore.Read(ShippedPath);
        var downloaded = CatalogStore.Read(DownloadedPath);

        if (shipped is null && downloaded is null)
        {
            Log.Error($"No catalog found at {ShippedPath} or {DownloadedPath}; the reciter list will be empty.");
            return Catalog.Empty;
        }

        if (downloaded is null)
            return shipped!;
        if (shipped is null)
            return downloaded;

        // A reinstall can ship a catalog newer than the one downloaded months ago.
        return downloaded.BuiltAt >= shipped.BuiltAt ? downloaded : shipped;
    }

    /// <summary>Stores a freshly downloaded catalog, replacing any earlier download.</summary>
    public void SaveDownloaded(Catalog catalog) => CatalogStore.Write(DownloadedPath, catalog);
}
