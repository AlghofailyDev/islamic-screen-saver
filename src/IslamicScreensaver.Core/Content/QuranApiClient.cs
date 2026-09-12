using System.Net.Http.Headers;

namespace IslamicScreensaver.Core.Content;

/// <summary>HTTP front for Quran.com API v4. Thin by design: fetch a string, hand it to <see cref="QuranApiParser"/>.</summary>
public sealed class QuranApiClient : IQuranApi
{
    public const string DefaultBaseUrl = "https://api.quran.com/api/v4/";

    private readonly HttpClient _http;

    public QuranApiClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress ??= new Uri(DefaultBaseUrl);
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IslamicScreensaver", "1.0"));
    }

    public async Task<IReadOnlyList<Reciter>> GetRecitersAsync(CancellationToken ct = default) =>
        QuranApiParser.ParseReciters(await GetAsync("resources/recitations?language=en", ct));

    public async Task<IReadOnlyList<Chapter>> GetChaptersAsync(CancellationToken ct = default) =>
        QuranApiParser.ParseChapters(await GetAsync("chapters?language=en", ct));

    public async Task<IReadOnlyList<TranslationResource>> GetTranslationsAsync(CancellationToken ct = default) =>
        QuranApiParser.ParseTranslations(await GetAsync("resources/translations?language=en", ct));

    public async Task<SurahAudio> GetSurahAudioAsync(int reciterId, int surah, CancellationToken ct = default) =>
        QuranApiParser.ParseSurahAudio(
            await GetAsync($"chapter_recitations/{reciterId}/{surah}?segments=true", ct), reciterId, surah);

    public async Task<IReadOnlyList<VerseText>> GetSurahTextAsync(int surah, CancellationToken ct = default) =>
        QuranApiParser.ParseSurahText(
            await GetAsync($"verses/by_chapter/{surah}?words=true&per_page=all&fields=text_uthmani&word_fields=text_uthmani", ct));

    public async Task<SurahTranslation> GetSurahTranslationAsync(int resourceId, int surah, CancellationToken ct = default) =>
        QuranApiParser.ParseSurahTranslation(
            await GetAsync($"quran/translations/{resourceId}?chapter_number={surah}", ct), resourceId, surah);

    private async Task<string> GetAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(relativeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
