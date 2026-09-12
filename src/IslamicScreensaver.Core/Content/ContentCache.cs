using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// The local content store the screensaver reads from and the settings app writes to. A manifest
/// records what is complete so playlist building is one file read, never a directory scan.
/// </summary>
/// <remarks>
/// Layout under the cache root:
/// <code>
/// manifest.json
/// catalog/reciters.json | chapters.json | translations.json
/// text/{surah}.json
/// translations/{resourceId}/{surah}.json
/// audio/{reciterId}/{surah}.mp3
/// audio/{reciterId}/{surah}.timings.json
/// </code>
/// </remarks>
public sealed class ContentCache
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _manifestGate = new();
    private CacheManifest _manifest;

    public ContentCache(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        _manifest = LoadManifest();
    }

    public static ContentCache Default() => new(AppPaths.CacheDirectory);

    public string RootDirectory { get; }

    public string ManifestPath => Path.Combine(RootDirectory, "manifest.json");

    // --- Paths -------------------------------------------------------------------------------

    public string AudioPath(int reciterId, int surah) => Path.Combine(RootDirectory, "audio", reciterId.ToString(), $"{surah}.mp3");

    public string TimingsPath(int reciterId, int surah) => Path.Combine(RootDirectory, "audio", reciterId.ToString(), $"{surah}.timings.json");

    public string TextPath(int surah) => Path.Combine(RootDirectory, "text", $"{surah}.json");

    public string TranslationPath(int resourceId, int surah) => Path.Combine(RootDirectory, "translations", resourceId.ToString(), $"{surah}.json");

    private string CatalogPath(string name) => Path.Combine(RootDirectory, "catalog", $"{name}.json");

    // --- Manifest queries -------------------------------------------------------------------

    public bool HasSurahAudio(int reciterId, int surah)
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.TryGetValue(reciterId, out var surahs) && surahs.Contains(surah);
    }

    public bool HasSurahText(int surah)
    {
        lock (_manifestGate)
            return _manifest.TextSurahs.Contains(surah);
    }

    public bool HasSurahTranslation(int resourceId, int surah)
    {
        lock (_manifestGate)
            return _manifest.TranslationsByResource.TryGetValue(resourceId, out var surahs) && surahs.Contains(surah);
    }

    /// <summary>Surahs for which this reciter's audio and timings are both complete.</summary>
    public IReadOnlyCollection<int> CachedSurahsFor(int reciterId)
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.TryGetValue(reciterId, out var surahs) ? surahs.ToArray() : [];
    }

    public IReadOnlyCollection<int> CachedReciters()
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToArray();
    }

    // --- Reads ------------------------------------------------------------------------------

    public IReadOnlyList<VerseText>? ReadSurahText(int surah) => ReadJson<List<VerseText>>(TextPath(surah));

    public SurahTranslation? ReadSurahTranslation(int resourceId, int surah) => ReadJson<SurahTranslation>(TranslationPath(resourceId, surah));

    public IReadOnlyList<VerseTiming>? ReadTimings(int reciterId, int surah) => ReadJson<List<VerseTiming>>(TimingsPath(reciterId, surah));

    public IReadOnlyList<Reciter>? ReadReciters() => ReadJson<List<Reciter>>(CatalogPath("reciters"));

    public IReadOnlyList<Chapter>? ReadChapters() => ReadJson<List<Chapter>>(CatalogPath("chapters"));

    public IReadOnlyList<TranslationResource>? ReadTranslations() => ReadJson<List<TranslationResource>>(CatalogPath("translations"));

    // --- Writes -----------------------------------------------------------------------------

    public async Task WriteSurahTextAsync(int surah, IReadOnlyList<VerseText> verses, CancellationToken ct = default)
    {
        await WriteJsonAsync(TextPath(surah), verses, ct);
        UpdateManifest(m => m.TextSurahs.Add(surah));
    }

    public async Task WriteSurahTranslationAsync(SurahTranslation translation, CancellationToken ct = default)
    {
        await WriteJsonAsync(TranslationPath(translation.ResourceId, translation.Surah), translation, ct);
        UpdateManifest(m => m.SurahsFor(m.TranslationsByResource, translation.ResourceId).Add(translation.Surah));
    }

    /// <summary>Writes timings only. Call <see cref="MarkSurahAudioComplete"/> once the MP3 is in place too.</summary>
    public Task WriteTimingsAsync(int reciterId, int surah, IReadOnlyList<VerseTiming> timings, CancellationToken ct = default) =>
        WriteJsonAsync(TimingsPath(reciterId, surah), timings, ct);

    public void MarkSurahAudioComplete(int reciterId, int surah)
    {
        if (!File.Exists(AudioPath(reciterId, surah)) || !File.Exists(TimingsPath(reciterId, surah)))
            throw new InvalidOperationException($"Cannot mark {reciterId}/{surah} complete: audio or timings file missing.");

        UpdateManifest(m => m.SurahsFor(m.AudioByReciter, reciterId).Add(surah));
    }

    public Task WriteRecitersAsync(IReadOnlyList<Reciter> reciters, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("reciters"), reciters, ct);

    public Task WriteChaptersAsync(IReadOnlyList<Chapter> chapters, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("chapters"), chapters, ct);

    public Task WriteTranslationsAsync(IReadOnlyList<TranslationResource> translations, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("translations"), translations, ct);

    /// <summary>Removes a reciter's audio for one surah and forgets it in the manifest.</summary>
    public void DeleteSurahAudio(int reciterId, int surah)
    {
        UpdateManifest(m =>
        {
            if (m.AudioByReciter.TryGetValue(reciterId, out var surahs))
                surahs.Remove(surah);
        });
        TryDelete(AudioPath(reciterId, surah));
        TryDelete(TimingsPath(reciterId, surah));
    }

    // --- Internals --------------------------------------------------------------------------

    private CacheManifest LoadManifest()
    {
        try
        {
            if (File.Exists(ManifestPath))
                return JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(ManifestPath), Json) ?? new CacheManifest();
        }
        catch (Exception ex)
        {
            Log.Error("Cache manifest unreadable; starting empty.", ex);
        }
        return new CacheManifest();
    }

    private void UpdateManifest(Action<CacheManifest> mutate)
    {
        lock (_manifestGate)
        {
            mutate(_manifest);
            AtomicFile.WriteAllText(ManifestPath, JsonSerializer.Serialize(_manifest, Json));
        }
    }

    private T? ReadJson<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex)
        {
            Log.Error($"Unreadable cache file {path}.", ex);
            return null;
        }
    }

    private static Task WriteJsonAsync<T>(string path, T value, CancellationToken ct) =>
        AtomicFile.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json), ct);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { Log.Error($"Could not delete {path}.", ex); }
    }

    public sealed class CacheManifest
    {
        public int Version { get; set; } = 1;
        public SortedSet<int> TextSurahs { get; set; } = [];
        public Dictionary<int, SortedSet<int>> AudioByReciter { get; set; } = [];
        public Dictionary<int, SortedSet<int>> TranslationsByResource { get; set; } = [];

        internal SortedSet<int> SurahsFor(Dictionary<int, SortedSet<int>> map, int id)
        {
            if (!map.TryGetValue(id, out var set))
                map[id] = set = [];
            return set;
        }
    }
}
