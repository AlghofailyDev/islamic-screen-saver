using System.Text.Json;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.CatalogBuilder;

/// <summary>
/// Quran.com (API v4). Twelve recordings, all Ḥafṣ ʿan ʿĀṣim, all with word-level timings — the only
/// source that can light the word being recited. Its chapter list is aspirational, so every file is
/// asked for before it is published.
/// </summary>
internal sealed class QuranComSource(Fetcher fetcher, bool probe)
{
    private const string Api = "https://api.quran.com/api/v4";
    private const string Riwayah = "حفص عن عاصم";

    public async Task<List<Recitation>> BuildAsync()
    {
        var json = await fetcher.GetJsonAsync($"{Api}/resources/recitations?language=en");
        var listed = json.Items("recitations").ToList();
        if (listed.Count == 0)
        {
            Console.Error.WriteLine("  ! quran.com returned no recitations.");
            return [];
        }

        var recitations = new List<Recitation>();
        foreach (var item in listed)
        {
            if (item.Int("id") is not { } id)
                continue;

            var reciter = ReciterAliases.ForQuranCom(id);
            if (reciter is null)
            {
                Console.Error.WriteLine($"  ! quran.com recitation {id} " +
                    $"({item.Text("reciter_name")}) is not in the alias table; skipped.");
                continue;
            }

            var style = item.Text("style");
            var (surahs, template) = await AvailableSurahsAsync(id);
            if (surahs.Count == 0)
            {
                Console.Error.WriteLine($"  ! quran.com recitation {id} has no reachable audio; skipped.");
                continue;
            }

            recitations.Add(new Recitation(
                RecitationId.QuranCom(id),
                reciter,
                Riwayah,
                StyleArabic(style),
                style,
                TimingLevel.Word,
                surahs)
            {
                // Every Quran.com surah recording is timed word by word, and none opens with the
                // basmalah — except Al-Fatihah, where it is verse 1 and therefore timed like any other.
                TimedSurahs = surahs,
                AudioIncludesBasmalah = false,
                AudioUrlTemplate = template,
                // Timings come through the API client, which pages and reshapes them; no plain URL fits.
                TimingsUrlTemplate = null,
            });
        }
        return recitations;
    }

    /// <summary>The chapters whose audio the CDN will actually serve, and the URL pattern they share.</summary>
    private async Task<(IReadOnlyList<int> Surahs, string? Template)> AvailableSurahsAsync(int recitation)
    {
        var json = await fetcher.GetJsonAsync($"{Api}/chapter_recitations/{recitation}");

        // Some rows carry a null chapter or no url at all; those are entries the CDN never got.
        var listed = new Dictionary<int, string>();
        foreach (var file in json.Items("audio_files"))
        {
            if (file.Int("chapter_id") is { } chapter && file.Text("audio_url") is { Length: > 0 } url)
                listed[chapter] = url;
        }

        // Take the lowest surah as the sample: below 100 the padded and plain endings differ, so the
        // shape is unambiguous. A sample of, say, 100 would look padded either way.
        var template = listed.Count > 0 ? TemplateFrom(listed.OrderBy(p => p.Key).First()) : null;
        if (!probe)
            return ([.. listed.Keys.Order()], template);

        var present = new List<int>();
        var checks = listed.Select(async pair =>
        {
            if (await fetcher.ExistsAsync(pair.Value))
                lock (present) present.Add(pair.Key);
        });
        await Task.WhenAll(checks);

        var missing = listed.Count - present.Count;
        if (missing > 0)
            Console.WriteLine($"  quran.com {recitation}: {missing} listed surah(s) are not on the CDN.");
        present.Sort();
        return (present, template);
    }

    /// <summary>
    /// ".../murattal/7.mp3" becomes ".../murattal/{surah}.mp3". Quran.com uses both a plain number
    /// and a zero-padded one depending on the recording, so both endings are tried.
    /// </summary>
    private static string? TemplateFrom(KeyValuePair<int, string> sample)
    {
        foreach (var (tail, placeholder) in new[]
                 {
                     ($"/{sample.Key:000}.mp3", "/{surah:000}.mp3"),
                     ($"/{sample.Key}.mp3", "/{surah}.mp3"),
                 })
        {
            if (sample.Value.EndsWith(tail, StringComparison.Ordinal))
                return sample.Value[..^tail.Length] + placeholder;
        }

        Console.Error.WriteLine($"  ! unrecognised audio URL shape: {sample.Value}");
        return null;
    }

    /// <summary>Quran.com names the style in English only; the catalog is read in Arabic too.</summary>
    private static string? StyleArabic(string? style) => style switch
    {
        "Murattal" => "مرتل",
        "Mujawwad" => "مجود",
        "Muallim" => "المصحف المعلم",
        _ => null,
    };
}
