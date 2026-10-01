using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Screensaver;
using IslamicScreensaver.Seeding;
using IslamicScreensaver.Settings;

namespace IslamicScreensaver;

/// <summary>
/// One binary, three modes. Windows launches the <c>.scr</c> with <c>/s</c> (run), <c>/c</c> (settings)
/// or <c>/p &lt;hwnd&gt;</c> (preview); double-clicking it passes nothing, which also opens settings.
/// </summary>
public partial class App : Application
{
    // Held so they are not collected while the app runs.
    private ScreensaverSession? _session;
    private PreviewHost? _preview;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var args = ScreensaverArgs.Parse(e.Args);
        // The version first: a tester's log should say which build it came from.
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "dev";
        Log.Info($"Started {version} in {args.Mode} mode (args: \"{string.Join(' ', e.Args)}\").");

        switch (args.Mode)
        {
            case ScreensaverMode.Run:
                _session = new ScreensaverSession();
                _session.Start();
                break;

            case ScreensaverMode.Preview:
                _preview = PreviewHost.TryStart(args.ParentWindow);
                if (_preview is null)
                    Shutdown();
                break;

            case ScreensaverMode.Configure:
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                var window = new SettingsWindow();
                if (args.ParentWindow != 0)
                    new WindowInteropHelper(window).Owner = args.ParentWindow;
                MainWindow = window;
                window.Show();
                break;

            case ScreensaverMode.Seed:
                _ = SeedThenExitAsync();
                break;

            case ScreensaverMode.Purge:
                Shutdown(Purge());
                break;

            case ScreensaverMode.Library:
                Shutdown(ChooseLibrary(args.Value));
                break;
        }
    }

    /// <summary>
    /// Records the library folder picked on setup's storage page. Run before the starter recitations
    /// download, so they land in the chosen place. A refused folder leaves the default in force.
    /// </summary>
    private static int ChooseLibrary(string? chosenFolder)
    {
        var existed = File.Exists(AppPaths.SettingsFile);
        var settings = SettingsStore.Load();
        string? problem = null;

        var result = LibraryChoice.Apply(settings, existed, chosenFolder, AppPaths.CacheDirectory,
            target => problem = Core.Content.CacheMover.Validate(AppPaths.CacheDirectory, target));

        switch (result)
        {
            case LibraryChoiceResult.UsedChosen:
                SettingsStore.Save(settings);
                Log.Info($"Library will be kept at {settings.CacheDirectory}, as chosen during setup.");
                return 0;
            case LibraryChoiceResult.KeptExisting:
                Log.Info("Setup offered a library folder, but settings already exist; the library stays where it is.");
                return 0;
            case LibraryChoiceResult.Refused:
                Log.Error($"The folder chosen during setup ({chosenFolder}) cannot be used: {problem ?? "not a valid path"}. Using the default.");
                return 1;
            default:
                Log.Info("Library will be kept in the default location.");
                return 0;
        }
    }

    /// <summary>
    /// Everything this app wrote, gone — including a library moved to another drive, which only these
    /// settings know the location of. Run by the uninstaller after the user says yes.
    /// </summary>
    private static int Purge()
    {
        var settings = SettingsStore.Load();
        var moved = settings.CacheDirectory;
        Log.Info($"Purging settings and downloads{(moved is null ? "" : $", including the library at {moved}")}.");

        // Nothing may be logged after this: every log line recreates the folder being deleted.
        var result = LibraryPurge.Run(AppPaths.SettingsDirectory, AppPaths.LocalDirectory, moved);
        return result.Failed.Count == 0 ? 0 : 1;
    }

    private async Task SeedThenExitAsync()
    {
        var exitCode = await StarterSeeder.RunAsync();
        Shutdown(exitCode);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // A crashed screensaver must never leave a black topmost window behind — log and get out.
        Log.Error("Unhandled exception.", e.Exception);
        e.Handled = true;
        Shutdown(1);
    }
}
