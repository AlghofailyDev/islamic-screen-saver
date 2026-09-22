using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;

namespace IslamicScreensaver.Settings;

/// <summary>Progress of one queued job, reported on the UI thread.</summary>
internal sealed record JobProgress(int Done, int Total, string? CurrentItem, double? CurrentFraction, int? CurrentSurah = null)
{
    public double Fraction => Total == 0 ? 0 : Math.Clamp((Done + (CurrentFraction ?? 0)) / Total, 0, 1);
}

/// <summary>
/// Runs content downloads one job at a time in the background and reports progress to the settings
/// UI. Jobs are keyed (e.g. "reciter:7", "translation:20") so a page can show the state of its own.
/// </summary>
internal sealed class DownloadManager
{
    private readonly ContentDownloader _downloader;
    private readonly ContentCache _cache;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Queue<Job> _queue = new();
    private readonly Dictionary<string, JobProgress> _active = new();
    private Job? _running;
    private CancellationTokenSource? _cts;

    private sealed record Job(string Key, Func<IProgress<JobProgress>, CancellationToken, Task> Work);

    public DownloadManager(ContentDownloader downloader, ContentCache cache)
    {
        _downloader = downloader;
        _cache = cache;
    }

    /// <summary>Raised on the UI thread whenever a job's progress changes, starts, finishes or fails.</summary>
    public event Action<string, JobProgress?>? Changed;

    /// <summary>Raised on the UI thread with a message when a job fails.</summary>
    public event Action<string, string>? Failed;

    /// <summary>Raised on the UI thread when a job finished but has something to tell the user (e.g. skipped items).</summary>
    public event Action<string, string>? Notice;

    private void Report(string key, string message) => _dispatcher.InvokeAsync(() => Notice?.Invoke(key, message));

    public bool IsBusy(string key) => _active.ContainsKey(key);

    public JobProgress? ProgressOf(string key) => _active.GetValueOrDefault(key);

    /// <param name="alsoAlFatihah">
    /// Fetch Al-Fatihah too when it is missing: its first verse is the basmalah, which is played
    /// before other surahs in this reciter's own voice. It is under a megabyte.
    /// </param>
    public void EnqueueReciter(Recitation recitation, IReadOnlyList<int> surahs, bool alsoAlFatihah = false)
    {
        var id = recitation.Id;
        var wanted = surahs.ToList();

        // Only worth fetching where the basmalah has to come from this reciter's own Al-Fatihah.
        // A recording that opens every surah with one already has it.
        if (alsoAlFatihah && recitation.AudioIncludesBasmalah != true && !wanted.Contains(RecitationProgram.AlFatihah))
            wanted.Insert(0, RecitationProgram.AlFatihah);

        var pending = wanted.Where(s => !_cache.HasSurahAudio(id, s) && !_cache.IsUnavailable(id, s)).ToList();
        Enqueue($"reciter:{id}", async (progress, ct) =>
        {
            var unavailable = 0;
            for (var i = 0; i < pending.Count; i++)
            {
                var surah = pending[i];
                var done = i;
                var itemProgress = new Progress<DownloadProgress>(p =>
                    progress.Report(new JobProgress(done, pending.Count, $"Surah {surah}", p.Fraction, surah)));
                progress.Report(new JobProgress(done, pending.Count, $"Surah {surah}", 0, surah));
                await _downloader.EnsureSurahTextAsync(surah, ct);
                try
                {
                    await _downloader.EnsureSurahAudioAsync(recitation, surah, itemProgress, ct);
                }
                catch (ContentUnavailableException)
                {
                    // The server does not host this one; keep going with the rest and say so at the end.
                    unavailable++;
                }
            }
            progress.Report(new JobProgress(pending.Count, pending.Count, null, null));
            if (unavailable > 0)
                Report($"reciter:{id}", $"{unavailable} of {pending.Count} surah{(unavailable == 1 ? " is" : "s are")} not available from this reciter — the source lists them but does not host the audio. Everything else was downloaded.");
        });
    }

    public void EnqueueTranslation(int translationId, IReadOnlyList<int> surahs)
    {
        var pending = surahs.Where(s => !_cache.HasSurahTranslation(translationId, s)).ToList();
        Enqueue($"translation:{translationId}", async (progress, ct) =>
        {
            for (var i = 0; i < pending.Count; i++)
            {
                progress.Report(new JobProgress(i, pending.Count, $"Surah {pending[i]}", null));
                await _downloader.EnsureSurahTextAsync(pending[i], ct);
                await _downloader.EnsureSurahTranslationAsync(translationId, pending[i], ct);
            }
            progress.Report(new JobProgress(pending.Count, pending.Count, null, null));
        });
    }

    public void CancelAll()
    {
        _queue.Clear();
        _cts?.Cancel();
    }

    /// <summary>Plain words for the failures a user can actually act on; raw HTTP text helps nobody.</summary>
    private static string Describe(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => "Could not reach Quran.com. Check your internet connection and try again.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Quran.com is rate-limiting downloads right now. Wait a few minutes and try again.",
        HttpRequestException http when (int)(http.StatusCode ?? 0) >= 500 => "Quran.com is having trouble serving this right now. Try again later.",
        IOException io => $"Could not write to the download folder: {io.Message}",
        _ => ex.Message,
    };

    private void Enqueue(string key, Func<IProgress<JobProgress>, CancellationToken, Task> work)
    {
        if (_active.ContainsKey(key))
            return;

        _active[key] = new JobProgress(0, 1, "Queued", null);
        Changed?.Invoke(key, _active[key]);
        _queue.Enqueue(new Job(key, work));
        _ = PumpAsync();
    }

    private async Task PumpAsync()
    {
        if (_running is not null)
            return;

        while (_queue.Count > 0)
        {
            _running = _queue.Dequeue();
            _cts = new CancellationTokenSource();
            var key = _running.Key;
            var progress = new Progress<JobProgress>(p =>
            {
                _active[key] = p;
                Changed?.Invoke(key, p);
            });

            try
            {
                await _running.Work(progress, _cts.Token);
                Log.Info($"Download job {key} finished.");
            }
            catch (OperationCanceledException)
            {
                Log.Info($"Download job {key} cancelled.");
            }
            catch (Exception ex)
            {
                Log.Error($"Download job {key} failed.", ex);
                await _dispatcher.InvokeAsync(() => Failed?.Invoke(key, Describe(ex)));
            }
            finally
            {
                _active.Remove(key);
                await _dispatcher.InvokeAsync(() => Changed?.Invoke(key, null));
                _running = null;
            }
        }
    }
}
