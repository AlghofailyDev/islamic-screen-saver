namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Write-to-temp-then-rename so a crash or power cut mid-write never leaves a half-written file where
/// the screensaver expects a complete one.
/// </summary>
public static class AtomicFile
{
    public static async Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, contents, ct);
        File.Move(temp, path, overwrite: true);
    }

    public static void WriteAllText(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }
}
