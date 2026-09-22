using System.Net;
using System.Text.Json;

namespace IslamicScreensaver.CatalogBuilder;

/// <summary>
/// The one place this tool talks to the network. Retries the failures worth retrying, caches every
/// response on disk so a re-run after a crash costs nothing, and keeps a lid on concurrency so we
/// stay a polite guest on two free APIs.
/// </summary>
internal sealed class Fetcher : IDisposable
{
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate;
    private readonly string? _cacheDir;
    private int _fetched;
    private int _fromCache;

    public Fetcher(int concurrency, string? cacheDir)
    {
        _http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("IslamicScreensaver-CatalogBuilder/1.0");
        _gate = new SemaphoreSlim(concurrency);
        _cacheDir = cacheDir;
        if (cacheDir is not null)
            Directory.CreateDirectory(cacheDir);
    }

    public int Fetched => _fetched;
    public int FromCache => _fromCache;

    /// <summary>Fetches JSON, or returns null when the resource simply is not there.</summary>
    public async Task<JsonElement?> GetJsonAsync(string url)
    {
        var text = await GetStringAsync(url);
        if (text is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text, JsonOptions);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  ! {url} did not return JSON: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> GetStringAsync(string url)
    {
        var cacheFile = CacheFileFor(url);
        if (cacheFile is not null && File.Exists(cacheFile))
        {
            Interlocked.Increment(ref _fromCache);
            return await File.ReadAllTextAsync(cacheFile);
        }

        await _gate.WaitAsync();
        try
        {
            var text = await WithRetriesAsync(url, async () =>
            {
                using var response = await _http.GetAsync(url);
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            });

            Interlocked.Increment(ref _fetched);
            if (text is not null && cacheFile is not null)
                await File.WriteAllTextAsync(cacheFile, text);
            return text;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// True when the file is really on the CDN. Quran.com lists recordings its CDN does not host, so
    /// the only honest answer comes from asking for the bytes.
    /// </summary>
    public async Task<bool> ExistsAsync(string url)
    {
        await _gate.WaitAsync();
        try
        {
            return await WithRetriesAsync(url, async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                Interlocked.Increment(ref _fetched);
                return response.IsSuccessStatusCode;
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<T?> WithRetriesAsync<T>(string url, Func<Task<T?>> attempt)
    {
        for (var tries = 1; ; tries++)
        {
            try
            {
                return await attempt();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && tries < 4)
            {
                await Task.Delay(TimeSpan.FromSeconds(tries * 2));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  ! {url}: {ex.Message}");
                return default;
            }
        }
    }

    private string? CacheFileFor(string url)
    {
        if (_cacheDir is null)
            return null;
        var name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(url)))[..16];
        return Path.Combine(_cacheDir, name + ".json");
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
