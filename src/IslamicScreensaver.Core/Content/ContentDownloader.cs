using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace IslamicScreensaver.Core.Content;

public sealed record DownloadProgress(string Item, long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : null;
}

/// <summary>
/// The server advertises this content but does not host it (404). Not a failure to retry — the item is
/// simply not obtainable, so callers skip it and carry on with the rest.
/// </summary>
public sealed class ContentUnavailableException(string message) : Exception(message);

/// <summary>
/// Fills the <see cref="ContentCache"/> from the API. Every method is idempotent: already-cached
/// items are skipped, and a partially downloaded MP3 resumes from where it stopped.
/// </summary>
public sealed class ContentDownloader
{
    private const int MaxAttempts = 4;

    private readonly IQuranApi _api;
    private readonly IAudioSource _audio;
    private readonly HttpClient _files;
    private readonly ContentCache _cache;

    /// <param name="files">Client for raw file downloads (the audio host, not the API).</param>
    /// <param name="audio">Where recordings come from. Defaults to Quran.com only.</param>
    public ContentDownloader(IQuranApi api, HttpClient files, ContentCache cache, IAudioSource? audio = null)
    {
        _api = api;
        _audio = audio ?? new QuranComAudioSource(api);
        _files = files;
        _cache = cache;
    }

    /// <summary>First retry waits this long; each further retry doubles it.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);

    public async Task RefreshCatalogAsync(CancellationToken ct = default)
    {
        await _cache.WriteRecitersAsync(await _api.GetRecitersAsync(ct), ct);
        await _cache.WriteChaptersAsync(await _api.GetChaptersAsync(ct), ct);
        await _cache.WriteTranslationsAsync(await _api.GetTranslationsAsync(ct), ct);
        _cache.MarkCatalogFetched();
    }

    public async Task EnsureSurahTextAsync(int surah, CancellationToken ct = default)
    {
        if (_cache.HasSurahText(surah))
            return;

        var verses = await WithRetry(() => _api.GetSurahTextAsync(surah, ct), $"text {surah}", ct);
        await _cache.WriteSurahTextAsync(surah, verses, ct);
    }

    public async Task EnsureSurahTranslationAsync(int resourceId, int surah, CancellationToken ct = default)
    {
        if (_cache.HasSurahTranslation(resourceId, surah))
            return;

        var translation = await WithRetry(() => _api.GetSurahTranslationAsync(resourceId, surah, ct), $"translation {resourceId}/{surah}", ct);
        await _cache.WriteSurahTranslationAsync(translation, ct);
    }

    /// <summary>
    /// Downloads one surah's audio and timings.
    /// </summary>
    /// <exception cref="ContentUnavailableException">
    /// The server does not host this recording (404). It is remembered as unavailable so it is not tried again.
    /// </exception>
    public async Task EnsureSurahAudioAsync(Recitation recitation, int surah, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        var id = recitation.Id;
        if (_cache.HasSurahAudio(id, surah))
            return;

        try
        {
            var audio = await WithRetry(() => _audio.GetSurahAudioAsync(recitation, surah, ct), $"timings {id}/{surah}", ct);

            // Timings are written first and the manifest is marked last, so a surah is never counted
            // as present with only half of it on disk. An untimed recording writes an empty list, which
            // still satisfies the pair the disk scan looks for.
            await _cache.WriteTimingsAsync(id, surah, audio.Timings, ct);

            var target = _cache.AudioPath(id, surah);
            var label = $"audio {id}/{surah}";
            await WithRetry(async () =>
            {
                await DownloadFileAsync(audio.AudioUrl, target, audio.FileSize, label, progress, ct);
                return true;
            }, label, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // A source can list a recording its CDN does not actually serve. Remember and move on.
            _cache.MarkUnavailable(id, surah);
            Log.Info($"Surah {surah} is not hosted for {id}; marked unavailable.");
            throw new ContentUnavailableException($"Surah {surah} is not available from this reciter.");
        }

        _cache.MarkSurahAudioComplete(id, surah);
    }

    /// <summary>
    /// Streams <paramref name="url"/> to <paramref name="target"/> via a <c>.part</c> file, resuming with a
    /// Range request if a partial exists. Only a complete transfer is renamed into place. Completeness is
    /// judged by the server's Content-Length; <paramref name="sizeHint"/> (from API metadata, which can be
    /// stale) only feeds progress when the server gives no length.
    /// </summary>
    private async Task DownloadFileAsync(string url, string target, long? sizeHint, string label, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var part = target + ".part";
        var existing = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
            request.Headers.Range = new RangeHeaderValue(existing, null);

        using var response = await _files.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        long offset;
        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            offset = existing;
        }
        else if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            // The server has nothing past what we hold: the partial is the whole file.
            File.Move(part, target, overwrite: true);
            return;
        }
        else
        {
            response.EnsureSuccessStatusCode();
            offset = 0; // Server ignored the range: start over.
        }

        long? expectedTotal = response.Content.Headers.ContentLength is { } len ? offset + len : null;
        var progressTotal = expectedTotal ?? sizeHint;

        await using (var file = new FileStream(part, offset > 0 ? FileMode.Open : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
        {
            file.Seek(offset, SeekOrigin.Begin);
            file.SetLength(offset);

            await using var body = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[1 << 16];
            var received = offset;
            int read;
            while ((read = await body.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                progress?.Report(new DownloadProgress(label, received, progressTotal));
            }
        }

        var length = new FileInfo(part).Length;
        if (expectedTotal is { } expected && length < expected)
            throw new IOException($"Download ended early: {length} of {expected} bytes.");

        File.Move(part, target, overwrite: true);
    }

    private async Task<T> WithRetry<T>(Func<Task<T>> action, string label, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            // A 404 means the content is not there; retrying cannot change that.
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                var delay = RetryBaseDelay * Math.Pow(2, attempt - 1);
                Log.Error($"{label}: attempt {attempt} failed, retrying in {delay.TotalSeconds:0}s.", ex);
                await Task.Delay(delay, ct);
            }
        }
    }
}
