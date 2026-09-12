using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;
using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Interop;
using IslamicScreensaver.Player;
using static IslamicScreensaver.Interop.NativeMethods;

namespace IslamicScreensaver.Screensaver;

/// <summary>
/// The running screensaver: one window per monitor, one playback engine, one shared exit policy, one
/// shutdown. Input from any window funnels through here so movement across monitors is measured consistently.
/// </summary>
internal sealed class ScreensaverSession
{
    private readonly List<ScreensaverWindow> _windows = [];
    private readonly InputExitPolicy _exitPolicy = InputExitPolicy.Default;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly AppSettings _settings = SettingsStore.Load();
    private PlaybackEngine? _engine;
    private bool _exiting;

    public void Start()
    {
        var monitors = DisplayMonitor.All();
        Log.Info($"Screensaver starting on {monitors.Count} monitor(s).");

        foreach (var monitor in monitors)
        {
            var window = new ScreensaverWindow(monitor, this);
            _windows.Add(window);
            window.Show();
        }

        // Keyboard input needs a focused window; the primary is first in the list.
        _windows.FirstOrDefault()?.Activate();

        // When the grace period ends, take the cursor's resting position as the origin so the first
        // real event is compared against where the mouse actually is, not against a synthetic move.
        var originTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = _exitPolicy.GracePeriod };
        originTimer.Tick += (_, _) =>
        {
            originTimer.Stop();
            if (GetCursorPos(out var p))
                OnMouseMove(p.X, p.Y);
        };
        originTimer.Start();

        StartPlayback();
    }

    private void StartPlayback()
    {
        var cache = ContentCache.Default();
        var playlist = PlaylistBuilder.Build(_settings, cache.CachedSurahsFor, cache.CachedReciters());
        var panels = PanelsToDrive();

        if (playlist.Count == 0)
        {
            Log.Info("Nothing cached to play; showing hint.");
            foreach (var panel in panels)
                panel.ShowHint("No recitations downloaded yet — open the screensaver's Settings to choose a reciter.");
            return;
        }

        var lastState = PlaybackState.Load();
        var startIndex = PlaylistBuilder.StartIndex(playlist, _settings, lastState);
        Log.Info($"Playlist: {playlist.Count} surah(s), starting at #{startIndex} ({playlist[startIndex].ReciterId}/{playlist[startIndex].Surah}).");

        _engine = new PlaybackEngine(cache, _settings, playlist, startIndex, lastState);
        _engine.VerseChanged += verse =>
        {
            foreach (var panel in panels)
                panel.Show(verse);
            _engine.CurrentState?.Save();
        };
        _engine.WordChanged += word =>
        {
            foreach (var panel in panels)
                panel.HighlightWord(word);
        };
        foreach (var panel in panels)
            panel.WordHighlight = _settings.Panel.WordHighlight;

        _engine.Start();
    }

    private List<VersePanel> PanelsToDrive() =>
        (_settings.MirrorOnAllMonitors ? _windows : _windows.Take(1)).Select(w => w.Panel).ToList();

    public void OnMouseMove(double screenX, double screenY)
    {
        if (_exitPolicy.ShouldExitOnMouseMove(_clock.Elapsed, screenX, screenY))
            Exit($"mouse moved to ({screenX:0}, {screenY:0})");
    }

    public void OnDiscreteInput(string description)
    {
        if (_exitPolicy.ShouldExitOnDiscreteInput())
            Exit(description);
    }

    public void Exit(string reason)
    {
        if (_exiting)
            return;

        _exiting = true;
        var state = _engine?.CurrentState;
        var heard = _engine?.HeardPosition;
        state?.Save();
        _engine?.Dispose();
        Log.Info($"Screensaver exiting ({reason}) after {_clock.Elapsed:mm\\:ss\\.fff}{(state is null ? "" : $" at {state.ReciterId}/{state.Surah} verse index {state.VerseIndex}, audio position {heard?.TotalSeconds:0.00}s")}.");
        Application.Current.Shutdown();
    }
}
