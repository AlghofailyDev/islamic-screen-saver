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
}

/// <summary>
/// The command line Windows hands a <c>.scr</c>. Windows is inconsistent about the shape —
/// <c>/c</c>, <c>/c:1234</c>, <c>/p 1234</c>, <c>-s</c>, upper or lower case — so all of it is accepted.
/// </summary>
public sealed record ScreensaverArgs(ScreensaverMode Mode, nint ParentWindow)
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
