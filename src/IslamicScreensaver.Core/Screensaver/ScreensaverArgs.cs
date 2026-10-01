using System.Globalization;

namespace IslamicScreensaver.Core.Screensaver;

public enum ScreensaverMode
{
    /// <summary>Run fullscreen (<c>/s</c>).</summary>
    Run,

    /// <summary>Open the settings window (<c>/c</c>, <c>/c:&lt;hwnd&gt;</c>, or no arguments).</summary>
    Configure,

    /// <summary>Render a thumbnail into the Windows dialog's preview box (<c>/p &lt;hwnd&gt;</c>).</summary>
    Preview,

    /// <summary>Not a Windows mode: download the starter content set headlessly (<c>/seed</c>), used by the installer.</summary>
    Seed,

    /// <summary>
    /// Not a Windows mode: delete settings and every downloaded recitation, wherever the library was
    /// moved to (<c>/purge</c>). Called by the uninstaller when the user asks for everything gone.
    /// </summary>
    Purge,

    /// <summary>
    /// Not a Windows mode: put the library in the folder the user chose during setup
    /// (<c>/library "D:\Stuff"</c>). First install only — it never moves an existing library.
    /// </summary>
    Library,
}

/// <summary>
/// The command line Windows hands a <c>.scr</c>. Windows is inconsistent about the shape —
/// <c>/c</c>, <c>/c:1234</c>, <c>/p 1234</c>, <c>-s</c>, upper or lower case — so all of it is accepted.
/// </summary>
/// <param name="Value">The folder for <see cref="ScreensaverMode.Library"/>; unused otherwise.</param>
public sealed record ScreensaverArgs(ScreensaverMode Mode, nint ParentWindow, string? Value = null)
{
    public static ScreensaverArgs Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return new ScreensaverArgs(ScreensaverMode.Configure, 0);

        var flag = args[0].Trim().TrimStart('/', '-');
        string? inlineHandle = null;

        var colon = flag.IndexOf(':');
        if (colon >= 0)
        {
            inlineHandle = flag[(colon + 1)..];
            flag = flag[..colon];
        }

        var handleText = inlineHandle ?? (args.Count > 1 ? args[1] : null);
        var handle = ParseHandle(handleText);

        return flag.ToLowerInvariant() switch
        {
            "s" => new ScreensaverArgs(ScreensaverMode.Run, 0),
            "p" => new ScreensaverArgs(ScreensaverMode.Preview, handle),
            "c" => new ScreensaverArgs(ScreensaverMode.Configure, handle),
            "seed" => new ScreensaverArgs(ScreensaverMode.Seed, 0),
            "purge" => new ScreensaverArgs(ScreensaverMode.Purge, 0),
            // A path, not a handle: taken verbatim, so a folder with spaces arrives whole when quoted.
            "library" => new ScreensaverArgs(ScreensaverMode.Library, 0, args.Count > 1 ? args[1] : null),
            // "/a" (legacy password change) and anything unknown fall back to settings.
            _ => new ScreensaverArgs(ScreensaverMode.Configure, 0),
        };
    }

    private static nint ParseHandle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? (nint)value
            : 0;
    }
}
