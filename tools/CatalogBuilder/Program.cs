using System.Diagnostics;
using IslamicScreensaver.CatalogBuilder;
using IslamicScreensaver.Core.Content;

// Builds the catalog the app ships and publishes: every reciter we know of, their recordings across
// quran.com and mp3quran.net, and which surahs each recording really has. Run it, eyeball the diff,
// commit. Nothing here runs on a user's machine.

var options = Options.Parse(args);
if (options is null)
{
    Console.WriteLine("""
        Usage: dotnet run --project tools/CatalogBuilder -- [options]

          --out <path>      Where to write the catalog (default: assets/catalog/catalog.json)
          --no-probe        Trust quran.com's chapter list instead of asking the CDN for each file.
                            Much faster; publishes surahs that will 404 on download.
          --no-cache        Ignore the on-disk response cache and refetch everything.
        """);
    return 0;
}

var watch = Stopwatch.StartNew();
using var fetcher = new Fetcher(options.Concurrency, options.NoCache ? null : options.CacheDirectory);

Console.WriteLine("Reading mp3quran.net…");
var reciters = await new Mp3QuranSource(fetcher).BuildAsync();

Console.WriteLine("Reading quran.com…");
var quranCom = await new QuranComSource(fetcher, options.Probe).BuildAsync();

// Quran.com's twelve recordings join the person they belong to. Word timings first: when the same
// reciter appears in both sources, the recording that can light a word is the better default.
var byReciter = reciters.ToDictionary(r => r.Id);
var orphans = 0;
foreach (var group in quranCom.GroupBy(r => r.ReciterId))
{
    if (!byReciter.TryGetValue(group.Key, out var entry))
    {
        // The alias points at an mp3quran reciter with no timed recordings of their own.
        Console.Error.WriteLine($"  ! {group.Key} has quran.com recordings but no mp3quran entry; kept alone.");
        orphans++;
        byReciter[group.Key] = new ReciterEntry(group.Key, "", group.Key, [.. group]);
        continue;
    }
    byReciter[group.Key] = entry with { Recitations = [.. group, .. entry.Recitations] };
}

// A published file is read by eye and diffed in git, so the order is fixed rather than however the
// two APIs happened to answer. Within a reciter the recording the panel can follow best comes first,
// which is also the one the UI should offer by default.
var catalog = new Catalog(
    Catalog.CurrentVersion,
    DateTimeOffset.UtcNow,
    [.. byReciter.Values
        .OrderBy(r => r.NameEnglish, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.Id, StringComparer.Ordinal)
        .Select(r => r with
        {
            Recitations = [.. r.Recitations
                .OrderByDescending(x => x.Timing)
                .ThenBy(x => x.Id.Source)
                .ThenBy(x => x.Id.Number)],
        })]);

CatalogStore.Write(options.Output, catalog);

var recitations = catalog.AllRecitations.ToList();
Console.WriteLine($"""

    Wrote {options.Output}
      {catalog.Reciters.Count} reciter(s), {recitations.Count} recitation(s), {catalog.Reciters.Count(r => r.Recitations.Count > 1)} reciter(s) with more than one
      timings: {recitations.Count(r => r.Timing == TimingLevel.Word)} word, {recitations.Count(r => r.Timing == TimingLevel.Verse)} verse, {recitations.Count(r => r.Timing == TimingLevel.None)} none
      basmalah in the audio: {recitations.Count(r => r.AudioIncludesBasmalah == true)} yes, {recitations.Count(r => r.AudioIncludesBasmalah == false)} no, {recitations.Count(r => r.AudioIncludesBasmalah is null)} unknown
      {recitations.Sum(r => r.AvailableSurahs.Count)} surah(s) available, {recitations.Sum(r => r.TimedSurahs.Count)} of them timed
      riwāyāt: {string.Join(", ", recitations.Select(r => r.RiwayahArabic).Distinct().Take(12))}
      {fetcher.Fetched} request(s), {fetcher.FromCache} served from the local cache, in {watch.Elapsed.TotalSeconds:F0}s
    """);

// Not a failure: an alias can outlive the recording it points at, and recitation 8 (Minshawi
// Mujawwad) has been listed-but-absent on the CDN for as long as we have been asking. The row stays
// so the mapping is there the day it comes back.
var unmapped = ReciterAliases.MappedQuranComIds.Except(quranCom.Select(r => r.Id.Number)).ToList();
if (unmapped.Count > 0)
    Console.WriteLine($"  Alias rows whose quran.com recording is gone for now: {string.Join(", ", unmapped)}");

return orphans == 0 && catalog.Reciters.Count > 0 ? 0 : 1;

internal sealed record Options(string Output, bool Probe, bool NoCache, int Concurrency, string CacheDirectory)
{
    public static Options? Parse(string[] args)
    {
        var probe = true;
        var noCache = false;
        string? output = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length: output = args[++i]; break;
                case "--no-probe": probe = false; break;
                case "--no-cache": noCache = true; break;
                default: return null;
            }
        }

        return new Options(
            output ?? Path.Combine(RepoRoot(), "assets", "catalog", CatalogStore.FileName),
            probe,
            noCache,
            Concurrency: 8,
            CacheDirectory: Path.Combine(Path.GetTempPath(), "IslamicScreensaver.CatalogBuilder"));
    }

    /// <summary>Walks up from the binary until the solution file turns up, so the tool runs from anywhere.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IslamicScreensaver.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
