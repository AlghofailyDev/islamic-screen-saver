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
/// audio/{source}/{number}/{surah}.mp3            e.g. audio/qurancom/7/1.mp3
/// audio/{source}/{number}/{surah}.timings.json
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
        MigrateLegacyLayout();

        // An upgraded manifest is written back once, so the old shape is not re-read every launch.
        if (_manifest.Version < CurrentManifestVersion)
        {
            _manifest.Version = CurrentManifestVersion;
            UpdateManifest(_ => { });
        }

        RebuildIfEmptyButFilesExist();
    }

    private const int CurrentManifestVersion = 2;

    /// <summary>
    /// Before recitation ids, audio sat in <c>audio/{number}/</c> and every id meant Quran.com.
    /// Move those folders under <c>audio/qurancom/</c> once, so an upgrade keeps what was downloaded.
    /// </summary>
    private void MigrateLegacyLayout()
    {
        try
        {
            var audioRoot = Path.Combine(RootDirectory, "audio");
            if (!Directory.Exists(audioRoot))
                return;

            var moved = 0;
            foreach (var directory in Directory.GetDirectories(audioRoot))
            {
                var name = Path.GetFileName(directory);
                if (!int.TryParse(name, out var number) || number <= 0)
                    continue;   // already a source folder such as "qurancom"

                var target = Path.Combine(audioRoot, RecitationId.QuranComPrefix, name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (Directory.Exists(target))
                    continue;   // nothing to do; leave the old folder alone rather than merging blindly

                Directory.Move(directory, target);
                moved++;
            }

            if (moved > 0)
                Log.Info($"Moved {moved} reciter folder(s) under audio/{RecitationId.QuranComPrefix}/.");
        }
        catch (Exception ex)
        {
            Log.Error("Could not move the old audio folders; they will be re-downloaded if needed.", ex);
        }
    }

    public static ContentCache Default() => new(AppPaths.CacheDirectory);

    /// <summary>The cache the user has configured (or the default when they have not moved it).</summary>
    public static ContentCache For(Settings.AppSettings settings) => new(settings.ResolvedCacheDirectory);

    public string RootDirectory { get; }

    public string ManifestPath => Path.Combine(RootDirectory, "manifest.json");

    // --- Paths -------------------------------------------------------------------------------

    public string AudioPath(RecitationId recitation, int surah) => Path.Combine(RecitationDirectory(recitation), $"{surah}.mp3");

    public string TimingsPath(RecitationId recitation, int surah) => Path.Combine(RecitationDirectory(recitation), $"{surah}.timings.json");

    public string TextPath(int surah) => Path.Combine(RootDirectory, "text", $"{surah}.json");

    public string TranslationPath(int resourceId, int surah) => Path.Combine(RootDirectory, "translations", resourceId.ToString(), $"{surah}.json");

    private string CatalogPath(string name) => Path.Combine(RootDirectory, "catalog", $"{name}.json");

    /// <summary>Nested by source, because a recitation id's colon is not legal in a Windows path.</summary>
    private string RecitationDirectory(RecitationId recitation) =>
        Path.Combine(RootDirectory, "audio", recitation.FolderName, recitation.Number.ToString());

    // --- Manifest queries -------------------------------------------------------------------

    public bool HasSurahAudio(RecitationId recitation, int surah)
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.TryGetValue(recitation, out var surahs) && surahs.Contains(surah);
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

    /// <summary>
    /// Surahs the server advertises for a reciter but does not actually host (the file 404s).
    /// Remembered so the UI can grey them out and downloads never try them again.
    /// </summary>
    public bool IsUnavailable(RecitationId recitation, int surah)
    {
        lock (_manifestGate)
            return _manifest.UnavailableByReciter.TryGetValue(recitation, out var surahs) && surahs.Contains(surah);
    }

    public IReadOnlyCollection<int> UnavailableSurahsFor(RecitationId recitation)
    {
        lock (_manifestGate)
            return _manifest.UnavailableByReciter.TryGetValue(recitation, out var surahs) ? surahs.ToArray() : [];
    }

    public void MarkUnavailable(RecitationId recitation, int surah) =>
        UpdateManifest(m => m.SurahsFor(m.UnavailableByReciter, recitation).Add(surah));

    /// <summary>Forgets what was unavailable for a reciter, so a retry can find content the server has since fixed.</summary>
    public void ClearUnavailable(RecitationId recitation) =>
        UpdateManifest(m => m.UnavailableByReciter.Remove(recitation));

    /// <summary>Surahs for which this reciter's audio and timings are both complete.</summary>
    public IReadOnlyCollection<int> CachedSurahsFor(RecitationId recitation)
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.TryGetValue(recitation, out var surahs) ? surahs.ToArray() : [];
    }

    public IReadOnlyCollection<RecitationId> CachedRecitations()
    {
        lock (_manifestGate)
            return _manifest.AudioByReciter.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToArray();
    }

    // --- Reads ------------------------------------------------------------------------------

    public IReadOnlyList<VerseText>? ReadSurahText(int surah) => ReadJson<List<VerseText>>(TextPath(surah));

    public SurahTranslation? ReadSurahTranslation(int resourceId, int surah) => ReadJson<SurahTranslation>(TranslationPath(resourceId, surah));

    public IReadOnlyList<VerseTiming>? ReadTimings(RecitationId recitation, int surah) => ReadJson<List<VerseTiming>>(TimingsPath(recitation, surah));

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
    public Task WriteTimingsAsync(RecitationId recitation, int surah, IReadOnlyList<VerseTiming> timings, CancellationToken ct = default) =>
        WriteJsonAsync(TimingsPath(recitation, surah), timings, ct);

    public void MarkSurahAudioComplete(RecitationId recitation, int surah)
    {
        if (!File.Exists(AudioPath(recitation, surah)) || !File.Exists(TimingsPath(recitation, surah)))
            throw new InvalidOperationException($"Cannot mark {recitation}/{surah} complete: audio or timings file missing.");

        UpdateManifest(m => m.SurahsFor(m.AudioByReciter, recitation).Add(surah));
    }

    public Task WriteRecitersAsync(IReadOnlyList<Reciter> reciters, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("reciters"), reciters, ct);

    public Task WriteChaptersAsync(IReadOnlyList<Chapter> chapters, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("chapters"), chapters, ct);

    public Task WriteTranslationsAsync(IReadOnlyList<TranslationResource> translations, CancellationToken ct = default) => WriteJsonAsync(CatalogPath("translations"), translations, ct);

    /// <summary>When the reciter/chapter/translation lists were last fetched, for the daily background refresh.</summary>
    public DateTimeOffset? CatalogFetchedAt
    {
        get { lock (_manifestGate) return _manifest.CatalogFetchedAt; }
    }

    public void MarkCatalogFetched() => UpdateManifest(m => m.CatalogFetchedAt = DateTimeOffset.Now);

    public bool CatalogIsStale(TimeSpan maxAge) => CatalogFetchedAt is not { } at || DateTimeOffset.Now - at > maxAge;

    /// <summary>Removes a reciter's audio for one surah and forgets it in the manifest.</summary>
    public void DeleteSurahAudio(RecitationId recitation, int surah)
    {
        UpdateManifest(m =>
        {
            if (m.AudioByReciter.TryGetValue(recitation, out var surahs))
                surahs.Remove(surah);
        });
        TryDelete(AudioPath(recitation, surah));
        TryDelete(TimingsPath(recitation, surah));
    }

    // --- Internals --------------------------------------------------------------------------

    private CacheManifest LoadManifest()
    {
        try
        {
            if (!File.Exists(ManifestPath))
                return new CacheManifest();

            var text = File.ReadAllText(ManifestPath);

            // Version 1 keyed audio by a bare Quran.com number; 2 keys it by recitation id.
            // Those keys will not parse as the new type, so convert rather than losing the library.
            using (var document = JsonDocument.Parse(text))
            {
                if (document.RootElement.TryGetProperty(nameof(CacheManifest.Version), out var version) && version.GetInt32() < 2)
                    return UpgradeManifest(JsonSerializer.Deserialize<LegacyManifest>(text, Json) ?? new LegacyManifest());
            }

            return JsonSerializer.Deserialize<CacheManifest>(text, Json) ?? new CacheManifest();
        }
        catch (Exception ex)
        {
            Log.Error("Cache manifest unreadable; starting empty.", ex);
        }
        return new CacheManifest();
    }

    /// <summary>
    /// The manifest is an index, not the truth: the files are. If it says the library is empty while
    /// downloaded audio sits on disk — a lost or corrupt index — rebuild it by scanning rather than
    /// making the user download hundreds of megabytes again.
    /// </summary>
    private void RebuildIfEmptyButFilesExist()
    {
        try
        {
            if (_manifest.AudioByReciter.Count > 0)
                return;

            var audioRoot = Path.Combine(RootDirectory, "audio");
            if (!Directory.Exists(audioRoot))
                return;

            var found = ScanAudio(audioRoot);
            var text = ScanNumbered(Path.Combine(RootDirectory, "text"), "*.json");
            var translations = ScanTranslations();

            if (found.Count == 0 && text.Count == 0 && translations.Count == 0)
                return;

            UpdateManifest(m =>
            {
                m.AudioByReciter = found;
                if (m.TextSurahs.Count == 0)
                    m.TextSurahs = text;
                if (m.TranslationsByResource.Count == 0)
                    m.TranslationsByResource = translations;
            });

            var surahs = found.Sum(kv => kv.Value.Count);
            Log.Info($"Rebuilt the cache index from disk: {found.Count} recitation(s), {surahs} surah(s), {text.Count} text file(s).");
        }
        catch (Exception ex)
        {
            Log.Error("Could not rebuild the cache index from disk.", ex);
        }
    }

    /// <summary>A surah counts as present only when both its audio and its timings are on disk.</summary>
    private static Dictionary<RecitationId, SortedSet<int>> ScanAudio(string audioRoot)
    {
        var found = new Dictionary<RecitationId, SortedSet<int>>();

        foreach (var sourceDirectory in Directory.GetDirectories(audioRoot))
        {
            var prefix = Path.GetFileName(sourceDirectory);
            foreach (var recitationDirectory in Directory.GetDirectories(sourceDirectory))
            {
                if (!RecitationId.TryParse($"{prefix}:{Path.GetFileName(recitationDirectory)}", out var recitation))
                    continue;

                foreach (var mp3 in Directory.GetFiles(recitationDirectory, "*.mp3"))
                {
                    if (!int.TryParse(Path.GetFileNameWithoutExtension(mp3), out var surah) || surah is < 1 or > 114)
                        continue;
                    if (!File.Exists(Path.Combine(recitationDirectory, $"{surah}.timings.json")))
                        continue;

                    if (!found.TryGetValue(recitation, out var surahs))
                        found[recitation] = surahs = [];
                    surahs.Add(surah);
                }
            }
        }

        return found;
    }

    private static SortedSet<int> ScanNumbered(string directory, string pattern)
    {
        var numbers = new SortedSet<int>();
        if (!Directory.Exists(directory))
            return numbers;

        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            if (int.TryParse(Path.GetFileNameWithoutExtension(file), out var number))
                numbers.Add(number);
        }
        return numbers;
    }

    private Dictionary<int, SortedSet<int>> ScanTranslations()
    {
        var found = new Dictionary<int, SortedSet<int>>();
        var root = Path.Combine(RootDirectory, "translations");
        if (!Directory.Exists(root))
            return found;

        foreach (var directory in Directory.GetDirectories(root))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var resourceId))
                continue;
            var surahs = ScanNumbered(directory, "*.json");
            if (surahs.Count > 0)
                found[resourceId] = surahs;
        }
        return found;
    }

    private static CacheManifest UpgradeManifest(LegacyManifest old)
    {
        static Dictionary<RecitationId, SortedSet<int>> Requalify(Dictionary<int, SortedSet<int>> map) =>
            map.ToDictionary(kv => RecitationId.QuranCom(kv.Key), kv => kv.Value);

        var upgraded = new CacheManifest
        {
            TextSurahs = old.TextSurahs,
            TranslationsByResource = old.TranslationsByResource,
            AudioByReciter = Requalify(old.AudioByReciter),
            UnavailableByReciter = Requalify(old.UnavailableByReciter),
            CatalogFetchedAt = old.CatalogFetchedAt,
            Version = 1,   // bumped and written back by the constructor
        };

        Log.Info($"Upgraded the cache manifest: {upgraded.AudioByReciter.Count} recitation(s) requalified as Quran.com.");
        return upgraded;
    }

    /// <summary>The version-1 manifest shape, read once so an existing library survives the upgrade.</summary>
    private sealed class LegacyManifest
    {
        public int Version { get; set; } = 1;
        public SortedSet<int> TextSurahs { get; set; } = [];
        public Dictionary<int, SortedSet<int>> AudioByReciter { get; set; } = [];
        public Dictionary<int, SortedSet<int>> TranslationsByResource { get; set; } = [];
        public Dictionary<int, SortedSet<int>> UnavailableByReciter { get; set; } = [];
        public DateTimeOffset? CatalogFetchedAt { get; set; }
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
        /// <summary>2 keys audio by recitation id instead of a bare Quran.com number.</summary>
        public int Version { get; set; } = 2;

        public SortedSet<int> TextSurahs { get; set; } = [];
        public Dictionary<RecitationId, SortedSet<int>> AudioByReciter { get; set; } = [];
        public Dictionary<int, SortedSet<int>> TranslationsByResource { get; set; } = [];
        public Dictionary<RecitationId, SortedSet<int>> UnavailableByReciter { get; set; } = [];
        public DateTimeOffset? CatalogFetchedAt { get; set; }

        /// <summary>Generic over the key, because translations are keyed by resource id and audio by recitation.</summary>
        internal SortedSet<int> SurahsFor<TKey>(Dictionary<TKey, SortedSet<int>> map, TKey id) where TKey : notnull
        {
            if (!map.TryGetValue(id, out var set))
                map[id] = set = [];
            return set;
        }
    }
}
