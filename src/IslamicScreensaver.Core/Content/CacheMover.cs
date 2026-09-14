namespace IslamicScreensaver.Core.Content;

public sealed record MoveProgress(int FilesDone, int FilesTotal, long BytesDone, long BytesTotal, string? CurrentFile)
{
    public double Fraction => BytesTotal == 0 ? 1 : Math.Clamp((double)BytesDone / BytesTotal, 0, 1);
}

/// <summary>
/// Relocates the downloaded library to another folder. Copies first and only deletes the source once
/// everything has arrived, so an interrupted move never loses content.
/// </summary>
public static class CacheMover
{
    /// <summary>Rejects a destination that cannot hold the library, with a reason fit to show the user.</summary>
    public static string? Validate(string source, string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return "Choose a folder.";

        string full;
        try
        {
            full = Path.GetFullPath(destination);
        }
        catch (Exception ex)
        {
            return $"That path cannot be used: {ex.Message}";
        }

        if (IsInside(full, AppContext.BaseDirectory))
            return "Choose a folder outside the application folder, so an update cannot erase it.";

        if (IsInside(full, Path.GetFullPath(source)) && !PathsEqual(full, source))
            return "Choose a folder that is not inside the current one.";

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return "That drive is not available.";

        try
        {
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, ".write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            return $"That folder cannot be written to: {ex.Message}";
        }

        return null;
    }

    /// <summary>Copies every file from <paramref name="source"/> to <paramref name="destination"/>, then removes the source.</summary>
    public static async Task MoveAsync(string source, string destination, IProgress<MoveProgress>? progress = null, CancellationToken ct = default)
    {
        if (PathsEqual(source, destination) || !Directory.Exists(source))
            return;

        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(f => new FileInfo(f).Length);
        long copied = 0;

        for (var i = 0; i < files.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, files[i]);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            progress?.Report(new MoveProgress(i, files.Length, copied, totalBytes, relative));

            await using (var from = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true))
            await using (var to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                await from.CopyToAsync(to, ct);
            }

            copied += new FileInfo(files[i]).Length;
        }

        progress?.Report(new MoveProgress(files.Length, files.Length, totalBytes, totalBytes, null));

        // Only now is it safe to let go of the originals.
        try
        {
            Directory.Delete(source, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Error($"Content was copied to {destination} but the old folder {source} could not be removed.", ex);
        }
    }

    public static long SizeOf(string directory)
    {
        try
        {
            var dir = new DirectoryInfo(directory);
            return dir.Exists ? dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not measure {directory}.", ex);
            return 0;
        }
    }

    private static bool IsInside(string candidate, string parent)
    {
        var p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var c = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
