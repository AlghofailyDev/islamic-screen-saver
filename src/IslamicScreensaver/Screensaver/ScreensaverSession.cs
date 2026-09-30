using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;
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
        Strings.Use(_settings.Language);

        // Windows owns the idle timer, so quiet hours are enforced here rather than prevented: the
        // one thing we can do is decide what happens once it has already launched us.
        var quiet = _settings.Quiet.CoversNow();
        if (quiet && _settings.Quiet.Behaviour == QuietHoursBehaviour.DoNotStart)
        {
            Log.Info($"Quiet hours {_settings.Quiet.Describe()}: exiting without showing anything.");
            Application.Current.Shutdown();
            return;
        }

        var monitors = DisplayMonitor.All();
        Log.Info($"Screensaver starting on {monitors.Count} monitor(s)." +
                 (quiet ? $" Quiet hours {_settings.Quiet.Describe()}: {_settings.Quiet.Behaviour}." : string.Empty));

        var dark = quiet && _settings.Quiet.Behaviour == QuietHoursBehaviour.DarkAndSilent;

        foreach (var monitor in monitors)
        {
            var window = new ScreensaverWindow(monitor, this);
            _windows.Add(window);
            if (dark)
                window.ShowNothing();
            else
                window.ApplyAppearance(_settings, showPanel: _settings.MirrorOnAllMonitors || _windows.Count == 1);
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

        if (dark)
            return;   // nothing to play, and nothing to fall quiet later

        StartPlayback();
        ScheduleQuietHours();
    }

    /// <summary>
    /// Goes quiet part-way through a session that began before the window opened — started at 22:50,
    /// quiet at 23:00. It does not come back out again afterwards: recitation resuming by itself at
    /// seven in the morning would be its own surprise, and the session is over as soon as anyone
    /// touches the machine.
    /// </summary>
    private void ScheduleQuietHours()
    {
        if (_settings.Quiet.TimeUntilStart() is not { } until)
            return;

        Log.Info($"Quiet hours {_settings.Quiet.Describe()} begin in {until.TotalMinutes:0} minute(s).");
        var timer = new DispatcherTimer { Interval = until };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            GoQuiet();
        };
        timer.Start();
    }

    private void GoQuiet()
    {
        if (_exiting)
            return;

        Log.Info($"Quiet hours reached: {_settings.Quiet.Behaviour}.");
        switch (_settings.Quiet.Behaviour)
        {
            case QuietHoursBehaviour.DoNotStart:
            case QuietHoursBehaviour.DarkAndSilent:
                _engine?.Dispose();
                _engine = null;
                foreach (var window in _windows)
                    window.ShowNothing();
                break;

            case QuietHoursBehaviour.Silent:
                _engine?.Mute();
                break;
        }
    }

    private void StartPlayback()
    {
        var cache = ContentCache.For(_settings);
        var panels = PanelsToDrive();

        // The library can live on a drive that is not currently attached.
        if (!Directory.Exists(cache.RootDirectory))
        {
            Log.Error($"Download folder {cache.RootDirectory} is not available; nothing to play.");
            foreach (var panel in panels)
                panel.ShowHint(Strings.Get("screensaver.folderMissing"));
            return;
        }

        var playlist = PlaylistBuilder.Build(_settings, cache.CachedSurahsFor, cache.CachedRecitations());

        if (playlist.Count == 0)
        {
            Log.Info("Nothing cached to play; showing hint.");
            foreach (var panel in panels)
                panel.ShowHint(Strings.Get("screensaver.nothing"));
            return;
        }

        var lastState = PlaybackState.Load();
        var startIndex = PlaylistBuilder.StartIndex(playlist, _settings, lastState);
        Log.Info($"Playlist: {playlist.Count} surah(s), starting at #{startIndex} ({playlist[startIndex].Recitation}/{playlist[startIndex].Surah}).");

        // A local file, so reading it here does not break the rule that /s never touches the network.
        var catalog = new CatalogProvider(cache.RootDirectory).Load();

        var windows = WindowsWithPanels();
        _engine = new PlaybackEngine(cache, _settings, playlist, startIndex, lastState, catalog);

        // Already inside quiet hours and asked only for silence: the panel still follows along.
        if (_settings.Quiet.Behaviour == QuietHoursBehaviour.Silent && _settings.Quiet.CoversNow())
            _engine.Mute();
        _engine.VerseChanged += verse =>
        {
            foreach (var window in windows)
            {
                window.Panel.Show(verse);
                window.SetReciter(verse.ReciterName);
            }
            _engine.CurrentState?.Save();
        };
        _engine.WordChanged += word =>
        {
            foreach (var panel in panels)
                panel.HighlightWord(word);
        };
        _engine.VerseProgress += fraction =>
        {
            foreach (var panel in panels)
                panel.SetProgress(fraction);
        };

        _engine.Start();
    }

    private List<ScreensaverWindow> WindowsWithPanels() =>
        (_settings.MirrorOnAllMonitors ? _windows : _windows.Take(1)).ToList();

    private List<VersePanel> PanelsToDrive() => WindowsWithPanels().Select(w => w.Panel).ToList();

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
        Log.Info($"Screensaver exiting ({reason}) after {_clock.Elapsed:mm\\:ss\\.fff}{(state is null ? "" : $" at {state.RecitationId}/{state.Surah} verse index {state.VerseIndex}, audio position {heard?.TotalSeconds:0.00}s")}.");
        Application.Current.Shutdown();
    }
}
