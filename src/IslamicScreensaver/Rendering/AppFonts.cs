using System.IO;
using System.Windows.Media;

namespace IslamicScreensaver.Rendering;

/// <summary>
/// Typefaces for the verse panel. Quran fonts are not shipped (licensing is the user's call); any
/// <c>.ttf</c> dropped into <c>assets/fonts</c> next to the app is picked up, then Windows' own
/// Arabic faces are the fallback.
/// </summary>
internal static class AppFonts
{
    private const string ArabicFallbacks = "Traditional Arabic, Arabic Typesetting, Segoe UI";

    public static string FontsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "assets", "fonts");

    public static FontFamily Arabic { get; } = BuildArabic();

    public static FontFamily Ui { get; } = new("Segoe UI Variable Text, Segoe UI");

    private static FontFamily BuildArabic()
    {
        try
        {
            if (Directory.Exists(FontsDirectory) && Directory.EnumerateFiles(FontsDirectory, "*.ttf").Any())
            {
                // "./#Family" entries resolve against the base URI; anything not found falls through the list.
                const string names = "./#KFGQPC Uthmanic Script HAFS, ./#KFGQPC HAFS Uthmanic Script, ./#Amiri Quran, ./#Amiri, " + ArabicFallbacks;
                return new FontFamily(new Uri(FontsDirectory + Path.DirectorySeparatorChar), names);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error("Could not load app-local fonts; using system fallbacks.", ex);
        }
        return new FontFamily(ArabicFallbacks);
    }
}
