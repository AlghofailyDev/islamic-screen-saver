using System.Net;
using System.Net.Http.Headers;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class ContentDownloaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));
    private readonly byte[] _mp3 = Enumerable.Range(0, 10_000).Select(i => (byte)(i % 251)).ToArray();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task EnsureSurahAudio_DownloadsTimingsAndMp3_ThenMarksComplete()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3);
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };
        var reports = new List<DownloadProgress>();

        await downloader.EnsureSurahAudioAsync(7, 112, new SyncProgress(reports));

        Assert.True(cache.HasSurahAudio(7, 112));
        Assert.Equal(_mp3, await File.ReadAllBytesAsync(cache.AudioPath(7, 112)));
        Assert.False(File.Exists(cache.AudioPath(7, 112) + ".part"));
        Assert.Single(cache.ReadTimings(7, 112)!);
        Assert.Equal(_mp3.Length, reports[^1].BytesReceived);
        Assert.Equal(1.0, reports[^1].Fraction);
    }

    [Fact]
    public async Task EnsureSurahAudio_ResumesPartialDownloadWithRange()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3);
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };

        // Simulate an earlier attempt that died after 4000 bytes.
        var part = cache.AudioPath(7, 112) + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(part)!);
        await File.WriteAllBytesAsync(part, _mp3[..4000]);

        await downloader.EnsureSurahAudioAsync(7, 112);

        Assert.Equal(4000, server.LastRangeFrom);
        Assert.Equal(_mp3, await File.ReadAllBytesAsync(cache.AudioPath(7, 112)));
        Assert.True(cache.HasSurahAudio(7, 112));
    }

    [Fact]
    public async Task EnsureSurahAudio_RetriesAfterTransientFailure()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3) { FailFirstRequests = 1 };
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };

        await downloader.EnsureSurahAudioAsync(7, 112);

        Assert.Equal(2, server.Requests);
        Assert.True(cache.HasSurahAudio(7, 112));
    }

    [Fact]
    public async Task EnsureSurahAudio_SkipsWhenCached()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3);
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };

        await downloader.EnsureSurahAudioAsync(7, 112);
        await downloader.EnsureSurahAudioAsync(7, 112);

        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task MissingFile_IsMarkedUnavailable_NotRetried()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3) { NotFound = true };
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };

        await Assert.ThrowsAsync<ContentUnavailableException>(() => downloader.EnsureSurahAudioAsync(8, 1));

        Assert.Equal(1, server.Requests);                  // a 404 is final, so no retries
        Assert.True(cache.IsUnavailable(8, 1));
        Assert.False(cache.HasSurahAudio(8, 1));
        Assert.Equal([1], cache.UnavailableSurahsFor(8));

        // And it is remembered across instances, so the UI can grey it out.
        Assert.True(new ContentCache(_root).IsUnavailable(8, 1));
    }

    [Fact]
    public async Task ClearUnavailable_AllowsAnotherAttempt()
    {
        var cache = new ContentCache(_root);
        cache.MarkUnavailable(8, 1);
        cache.ClearUnavailable(8);
        Assert.False(cache.IsUnavailable(8, 1));
    }

    [Fact]
    public async Task TruncatedDownload_IsNotMarkedComplete()
    {
        var cache = new ContentCache(_root);
        var server = new FakeFileServer(_mp3) { TruncateTo = 5000, FailFirstRequests = 0 };
        var downloader = new ContentDownloader(new FakeApi(_mp3.Length), new HttpClient(server), cache) { RetryBaseDelay = TimeSpan.Zero };

        // Every attempt is truncated, so the downloader gives up after its retries.
        await Assert.ThrowsAsync<IOException>(() => downloader.EnsureSurahAudioAsync(7, 112));

        Assert.False(cache.HasSurahAudio(7, 112));
        Assert.False(File.Exists(cache.AudioPath(7, 112)));
    }

    private sealed class SyncProgress(List<DownloadProgress> sink) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => sink.Add(value);
    }

    private sealed class FakeApi(long fileSize) : IQuranApi
    {
        public Task<SurahAudio> GetSurahAudioAsync(int reciterId, int surah, CancellationToken ct = default) =>
            Task.FromResult(new SurahAudio(reciterId, surah, "https://files.test/audio.mp3", fileSize,
                [new VerseTiming(new VerseKey(surah, 1), 0, 1000, [])]));

        public Task<IReadOnlyList<Reciter>> GetRecitersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Chapter>> GetChaptersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranslationResource>> GetTranslationsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VerseText>> GetSurahTextAsync(int surah, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SurahTranslation> GetSurahTranslationAsync(int resourceId, int surah, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Serves one file with byte-range support, optionally failing or truncating to exercise retries.</summary>
    private sealed class FakeFileServer(byte[] file) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public long? LastRangeFrom { get; private set; }
        public int FailFirstRequests { get; init; }
        public int? TruncateTo { get; init; }
        public bool NotFound { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            if (Requests <= FailFirstRequests)
                throw new HttpRequestException("simulated network failure");
            if (NotFound)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") });

            var from = request.Headers.Range?.Ranges.FirstOrDefault()?.From;
            LastRangeFrom = from;
            var start = (int)(from ?? 0);
            var end = TruncateTo is { } t ? Math.Min(t, file.Length) : file.Length;
            var slice = file[start..Math.Max(start, end)];

            var response = new HttpResponseMessage(from is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(slice),
            };
            // Always advertise the full remaining length; a truncated body then looks like a dropped connection.
            response.Content.Headers.ContentLength = file.Length - start;
            if (from is not null)
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, file.Length - 1, file.Length);
            return Task.FromResult(response);
        }
    }
}
