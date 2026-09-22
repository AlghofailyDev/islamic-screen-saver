using System.Net;
using System.Net.Http;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Where one surah's audio and timings come from. Quran.com answers through its API; mp3quran.net is
/// a URL from the catalog and a timings endpoint. The downloader only ever sees this.
/// </summary>
public interface IAudioSource
{
    Task<SurahAudio> GetSurahAudioAsync(Recitation recitation, int surah, CancellationToken ct = default);
}

/// <summary>Quran.com: the audio URL and the word-level timings arrive in the same response.</summary>
public sealed class QuranComAudioSource(IQuranApi api) : IAudioSource
{
    public Task<SurahAudio> GetSurahAudioAsync(Recitation recitation, int surah, CancellationToken ct = default) =>
        api.GetSurahAudioAsync(recitation.Id.Number, surah, ct);
}

/// <summary>
/// mp3quran.net: the file sits at a predictable path under the reciter's server, and the timings come
/// from a separate endpoint. Both URLs are in the catalog, so nothing here has to know the API layout.
/// </summary>
public sealed class Mp3QuranAudioSource(HttpClient http) : IAudioSource
{
    public async Task<SurahAudio> GetSurahAudioAsync(Recitation recitation, int surah, CancellationToken ct = default)
    {
        var audioUrl = recitation.ResolveUrl(surah);
        if (string.IsNullOrEmpty(audioUrl))
            throw new ContentUnavailableException($"{recitation.Id} has no audio URL for surah {surah}.");

        var timingsUrl = recitation.ResolveTimingsUrl(surah);
        if (string.IsNullOrEmpty(timingsUrl) || !recitation.IsTimed(surah))
            return new SurahAudio(recitation.Id, surah, audioUrl, null, []);

        using var response = await http.GetAsync(timingsUrl, ct);

        // The catalog said this surah was timed and it no longer is: still worth downloading the
        // audio, just without a panel that follows it.
        if (response.StatusCode is HttpStatusCode.NotFound)
            return new SurahAudio(recitation.Id, surah, audioUrl, null, []);

        response.EnsureSuccessStatusCode();
        var timings = Mp3QuranParser.ParseTimings(await response.Content.ReadAsStringAsync(ct), surah);
        return new SurahAudio(recitation.Id, surah, audioUrl, null, timings);
    }
}

/// <summary>Picks the source a recitation belongs to.</summary>
public sealed class AudioSourceRouter(IAudioSource quranCom, IAudioSource mp3Quran) : IAudioSource
{
    public Task<SurahAudio> GetSurahAudioAsync(Recitation recitation, int surah, CancellationToken ct = default) =>
        For(recitation.Id.Source).GetSurahAudioAsync(recitation, surah, ct);

    private IAudioSource For(RecitationSource source) => source switch
    {
        RecitationSource.QuranCom => quranCom,
        RecitationSource.Mp3Quran => mp3Quran,
        _ => throw new NotSupportedException($"No audio source for {source}."),
    };
}
