namespace IslamicScreensaver.Core;

/// <summary>
/// Minimal append-only file log. The screensaver has no UI to surface errors in, so anything that goes
/// wrong lands in <c>%LOCALAPPDATA%\IslamicScreensaver\logs\</c>. Never throws.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();

    /// <summary>Where log files go. Tests point this at a scratch folder so they never touch the user's logs.</summary>
    public static string Directory { get; set; } = AppPaths.LogDirectory;

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var file = Path.Combine(Directory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(file, line);
            }
        }
        catch
        {
            // Logging must never take the screensaver down.
        }
    }
}
