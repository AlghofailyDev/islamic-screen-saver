using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Settings;

public sealed class LibraryPurgeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string Make(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Touch(string directory, string name) => File.WriteAllText(Path.Combine(directory, name), "x");

    [Theory]
    [InlineData(@"E:\Softwares\IslamicScreensaver\cache", @"E:\Softwares\IslamicScreensaver")]
    [InlineData(@"D:\Stuff\IslamicScreensaver\cache\", @"D:\Stuff\IslamicScreensaver")]      // trailing separator
    [InlineData(@"D:\stuff\islamicscreensaver\CACHE", @"D:\stuff\islamicscreensaver")]        // any case
    [InlineData(@"D:\IslamicScreensaver\cache", @"D:\IslamicScreensaver")]                    // picked a drive root
    public void TheShapeTheAppCreatesIsRecognised(string configured, string expected)
    {
        Assert.Equal(expected, LibraryPurge.MovedLibraryRoot(configured));
    }

    [Theory]
    [InlineData(@"D:\Music")]                        // a folder the user owns
    [InlineData(@"D:\Music\cache")]                  // right leaf, wrong parent
    [InlineData(@"D:\IslamicScreensaver")]           // right parent, no cache
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("\0not a path")]
    public void AnythingElseIsLeftAlone(string? configured)
    {
        // A hand-edited settings file must not be able to aim the uninstaller at someone's own files.
        Assert.Null(LibraryPurge.MovedLibraryRoot(configured));
    }

    [Fact]
    public void AMovedLibraryGoes_ButTheFolderTheUserPickedStays()
    {
        var picked = Make("E", "Softwares");
        var library = Make("E", "Softwares", "IslamicScreensaver", "cache", "audio");
        Touch(library, "1.mp3");
        Touch(picked, "the user's own file.txt");
        var settings = Make("AppData", "IslamicScreensaver");
        var local = Make("LocalAppData", "IslamicScreensaver");

        var result = LibraryPurge.Run(settings, local, Path.Combine(picked, "IslamicScreensaver", "cache"));

        Assert.False(Directory.Exists(Path.Combine(picked, "IslamicScreensaver")));
        Assert.True(File.Exists(Path.Combine(picked, "the user's own file.txt")));
        Assert.False(Directory.Exists(settings));
        Assert.False(Directory.Exists(local));
        Assert.Empty(result.Failed);
        Assert.Equal(3, result.Removed.Count);
    }

    [Fact]
    public void ALocationThatIsNotOursIsReportedAndKept()
    {
        var music = Make("Music");
        Touch(music, "song.mp3");
        var settings = Make("AppData", "IslamicScreensaver");

        var result = LibraryPurge.Run(settings, Make("LocalAppData", "IslamicScreensaver"), music);

        Assert.True(File.Exists(Path.Combine(music, "song.mp3")));
        Assert.Equal([music], result.LeftAlone);
        Assert.False(Directory.Exists(settings));   // everything that *is* ours still goes
    }

    [Fact]
    public void ALibraryThatWasNeverMovedGoesWithTheLocalFolder()
    {
        var local = Make("LocalAppData", "IslamicScreensaver");
        Touch(Make("LocalAppData", "IslamicScreensaver", "cache", "audio"), "1.mp3");

        var result = LibraryPurge.Run(Make("AppData", "IslamicScreensaver"), local, movedLibrary: null);

        Assert.False(Directory.Exists(local));
        Assert.Empty(result.LeftAlone);
    }

    [Fact]
    public void FoldersAlreadyGoneAreNotAnError()
    {
        // Running the uninstaller twice, or after deleting things by hand, must still succeed.
        var result = LibraryPurge.Run(
            Path.Combine(_root, "never-made"),
            Path.Combine(_root, "never-made-either"),
            Path.Combine(_root, "gone", "IslamicScreensaver", "cache"));

        Assert.Empty(result.Removed);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void ABusyFileIsReportedRatherThanStoppingTheRest()
    {
        var settings = Make("AppData", "IslamicScreensaver");
        var local = Make("LocalAppData", "IslamicScreensaver", "logs");
        var busy = Path.Combine(local, "today.log");
        File.WriteAllText(busy, "x");

        using var held = new FileStream(busy, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = LibraryPurge.Run(settings, Path.GetDirectoryName(local)!, movedLibrary: null);

        Assert.False(Directory.Exists(settings));   // the free folder still went
        Assert.Single(result.Failed);               // and the busy one was reported
    }

    [Theory]
    [InlineData("/purge")]
    [InlineData("-purge")]
    [InlineData("/PURGE")]
    public void TheUninstallerCanAskForIt(string flag)
    {
        Assert.Equal(ScreensaverMode.Purge, ScreensaverArgs.Parse([flag]).Mode);
    }
}
