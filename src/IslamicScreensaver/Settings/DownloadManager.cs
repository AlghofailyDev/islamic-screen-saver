using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;

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

    public bool IsBusy(string key) => _active.ContainsKey(key);

    public JobProgress? ProgressOf(string key) => _active.GetValueOrDefault(key);

    public void EnqueueReciter(int reciterId, IReadOnlyList<int> surahs)
    {
        var pending = surahs.Where(s => !_cache.HasSurahAudio(reciterId, s)).ToList();
        Enqueue($"reciter:{reciterId}", async (progress, ct) =>
        {
            for (var i = 0; i < pending.Count; i++)
            {
                var surah = pending[i];
                var done = i;
                var itemProgress = new Progress<DownloadProgress>(p =>
                    progress.Report(new JobProgress(done, pending.Count, $"Surah {surah}", p.Fraction, surah)));
                progress.Report(new JobProgress(done, pending.Count, $"Surah {surah}", 0, surah));
                await _downloader.EnsureSurahTextAsync(surah, ct);
                await _downloader.EnsureSurahAudioAsync(reciterId, surah, itemProgress, ct);
            }
            progress.Report(new JobProgress(pending.Count, pending.Count, null, null));
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
                await _dispatcher.InvokeAsync(() => Failed?.Invoke(key, ex.Message));
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
