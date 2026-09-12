using System.Runtime.CompilerServices;
using IslamicScreensaver.Core;

namespace IslamicScreensaver.Tests;

internal static class TestSetup
{
    /// <summary>Keeps test noise (retry failures, corrupt-manifest warnings) out of the user's real log folder.</summary>
    [ModuleInitializer]
    internal static void RedirectLog()
    {
        Log.Directory = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", "logs");
    }
}
