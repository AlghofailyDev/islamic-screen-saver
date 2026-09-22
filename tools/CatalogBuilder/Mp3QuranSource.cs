using System.Text.Json;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.CatalogBuilder;

/// <summary>
/// mp3quran.net (API v3). A reciter owns several "moshaf" — one per riwāyah and style — and a
/// separate endpoint says which surahs of which have ayah timings. Every recording is published,
/// timed or not: the variety is the reason for this source, and a recording the panel cannot follow
/// is still worth hearing. Each one says for itself which surahs can be followed.
/// </summary>
internal sealed class Mp3QuranSource(Fetcher fetcher)
{
    private const string Api = "https://mp3quran.net/api/v3";

    /// <summary>A moshaf id and a timing "read" id are the same number — verified across all 115 timed reads.</summary>
    public async Task<List<ReciterEntry>> BuildAsync()
    {
        var arabic = await ReadRecitersAsync("ar");
        var english = await ReadRecitersAsync("eng");
        var riwayat = await ReadRiwayatAsync();
        var timed = await ReadTimedSurahsAsync();
        Console.WriteLine($"  mp3quran: {arabic.Count} reciter(s), {timed.Count} with ayah timings.");

        var entries = new List<ReciterEntry>();
        foreach (var reciter in arabic.Values.OrderBy(r => r.Id))
        {
            var recitations = new List<Recitation>();
            foreach (var moshaf in reciter.Moshaf)
            {
                if (moshaf.Surahs.Count == 0)
                    continue;

                var timedSurahs = timed.TryGetValue(moshaf.Id, out var set)
                    ? moshaf.Surahs.Where(set.Contains).ToList()
                    : [];

                var englishName = english.TryGetValue(reciter.Id, out var en)
                    ? en.Moshaf.FirstOrDefault(m => m.Id == moshaf.Id)?.Name
                    : null;
                var (riwayah, style) = Riwayat.Describe(riwayat, moshaf.Rewaya, moshaf.Name, arabic: true);
                var (riwayahEn, styleEn) = Riwayat.Describe(riwayat, moshaf.Rewaya, englishName, arabic: false);

                recitations.Add(new Recitation(
                    RecitationId.Mp3Quran(moshaf.Id),
                    ReciterAliases.CanonicalId(reciter.Id),
                    riwayah,
                    style,
                    styleEn,
                    timedSurahs.Count > 0 ? TimingLevel.Verse : TimingLevel.None,
                    moshaf.Surahs)
                {
                    RiwayahEnglish = riwayahEn,
                    TimedSurahs = timedSurahs,
                    AudioUrlTemplate = moshaf.Server.TrimEnd('/') + "/{surah:000}.mp3",
                    TimingsUrlTemplate = timedSurahs.Count > 0
                        ? $"{Api}/ayat_timing?surah={{surah}}&read={moshaf.Id}"
                        : null,
                    AudioIncludesBasmalah = await ProbeBasmalahAsync(moshaf.Id, timedSurahs),
                });
            }

            if (recitations.Count == 0)
                continue;

            entries.Add(new ReciterEntry(
                ReciterAliases.CanonicalId(reciter.Id),
                reciter.Name,
                english.TryGetValue(reciter.Id, out var e) ? e.Name : reciter.Name,
                FillBasmalahGaps(recitations)));
        }
        return entries;
    }

    /// <summary>
    /// An untimed recording cannot be probed, so it borrows the answer from another recording by the
    /// same reciter. A reciter who opens with the basmalah does so throughout; where every one of
    /// their recordings is untimed the answer stays null and playback decides.
    /// </summary>
    private static List<Recitation> FillBasmalahGaps(List<Recitation> recitations)
    {
        if (recitations.Select(r => r.AudioIncludesBasmalah).OfType<bool>().Distinct().ToList() is not [var known])
            return recitations;

        return [.. recitations.Select(r =>
            r.AudioIncludesBasmalah is null ? r with { AudioIncludesBasmalah = known } : r)];
    }

    /// <summary>Anything before the first verse is long enough to be a basmalah and nothing else.</summary>
    private const int BasmalahLeadInMs = 1000;

    /// <summary>
    /// Whether this recording opens a surah with the basmalah. One timed surah that is neither
    /// Al-Fatihah (where the basmalah is verse 1) nor at-Tawbah (which has none) settles it.
    ///
    /// Two shapes mean yes. Some recordings label the basmalah "ayah 0"; the rest do not time it at
    /// all and simply start verse 1 several seconds in — Shatri at 7.9s, al-ʿAfasy at 6.7s. Reading
    /// only the ayah number would have called those "no basmalah" and had us play a second one.
    /// </summary>
    private async Task<bool?> ProbeBasmalahAsync(int read, IReadOnlyList<int> timedSurahs)
    {
        var surah = timedSurahs.FirstOrDefault(s => s is not (1 or 9));
        if (surah == 0)
            return null;

        var json = await fetcher.GetJsonAsync($"{Api}/ayat_timing?surah={surah}&read={read}");
        var first = json.Items().FirstOrDefault();
        if (first.Int("ayah") is not { } ayah)
            return null;

        return ayah == 0 || first.Int("start_time") >= BasmalahLeadInMs;
    }

    private async Task<Dictionary<int, Riwayah>> ReadRiwayatAsync()
    {
        var arabic = await fetcher.GetJsonAsync($"{Api}/riwayat?language=ar");
        var english = await fetcher.GetJsonAsync($"{Api}/riwayat?language=eng");

        var names = english.Items("riwayat")
            .Where(r => r.Int("id") is not null)
            .ToDictionary(r => r.Int("id")!.Value, r => r.Text("name") ?? "");

        return arabic.Items("riwayat")
            .Where(r => r.Int("id") is not null)
            .ToDictionary(
                r => r.Int("id")!.Value,
                r => new Riwayah(r.Int("id")!.Value, r.Text("name") ?? "", names.GetValueOrDefault(r.Int("id")!.Value, "")));
    }

    private async Task<Dictionary<int, MqReciter>> ReadRecitersAsync(string language)
    {
        var json = await fetcher.GetJsonAsync($"{Api}/reciters?language={language}");
        var reciters = new Dictionary<int, MqReciter>();

        foreach (var item in json.Items("reciters"))
        {
            if (item.Int("id") is not { } id)
                continue;

            var moshaf = new List<MqMoshaf>();
            foreach (var m in item.Items("moshaf"))
            {
                if (m.Int("id") is not { } moshafId || m.Text("server") is not { Length: > 0 } server)
                    continue;
                moshaf.Add(new MqMoshaf(
                    moshafId,
                    m.Text("name") ?? "",
                    m.Int("rewaya_id") ?? 0,
                    server,
                    SurahRange.Parse(m.Text("surah_list"))));
            }
            reciters[id] = new MqReciter(id, item.Text("name") ?? "", moshaf);
        }
        return reciters;
    }

    /// <summary>Read id → the surahs it actually has timings for. Some reads are listed but hold none.</summary>
    private async Task<Dictionary<int, HashSet<int>>> ReadTimedSurahsAsync()
    {
        var reads = await fetcher.GetJsonAsync($"{Api}/ayat_timing/reads");
        var ids = reads.Items().Select(r => r.Int("id")).OfType<int>().ToList();

        var timed = new Dictionary<int, HashSet<int>>();
        foreach (var id in ids)
        {
            var soar = await fetcher.GetJsonAsync($"{Api}/ayat_timing/soar?read={id}");
            timed[id] = [.. soar.Items().Select(s => s.Int("id")).OfType<int>()];
        }
        return timed;
    }

    private sealed record MqReciter(int Id, string Name, List<MqMoshaf> Moshaf);

    private sealed record MqMoshaf(int Id, string Name, int Rewaya, string Server, IReadOnlyList<int> Surahs);
}
