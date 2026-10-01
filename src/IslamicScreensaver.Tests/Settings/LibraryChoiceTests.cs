using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Screensaver;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Settings;

public sealed class LibraryChoiceTests
{
    private const string Default = @"C:\Users\someone\AppData\Local\IslamicScreensaver\cache";

    private static string? Fine(string _) => null;

    [Theory]
    [InlineData(@"E:\Softwares", @"E:\Softwares\IslamicScreensaver\cache")]
    [InlineData(@"E:\Softwares\", @"E:\Softwares\IslamicScreensaver\cache")]
    [InlineData(@"D:\", @"D:\IslamicScreensaver\cache")]                           // a whole drive: not "D:IslamicScreensaver"
    [InlineData(@"D:\IslamicScreensaver", @"D:\IslamicScreensaver\cache")]          // already ours: not nested again
    [InlineData(@"D:\IslamicScreensaver\cache", @"D:\IslamicScreensaver\cache")]
    [InlineData(@"D:\islamicscreensaver\CACHE", @"D:\islamicscreensaver\CACHE")]
    public void TheLibraryAlwaysGetsAFolderOfItsOwn(string chosen, string expected)
    {
        Assert.Equal(expected, AppPaths.LibraryInside(chosen));
    }

    [Fact]
    public void ThatShapeIsExactlyWhatPurgeWillRecogniseAndRemove()
    {
        // The two must agree, or an uninstall would leave a library behind.
        foreach (var chosen in new[] { @"E:\Softwares", @"D:\", @"D:\IslamicScreensaver" })
            Assert.NotNull(LibraryPurge.MovedLibraryRoot(AppPaths.LibraryInside(chosen)));
    }

    [Fact]
    public void AFreshInstallUsesTheChosenFolder()
    {
        var settings = new AppSettings();

        var result = LibraryChoice.Apply(settings, settingsExisted: false, @"E:\Softwares", Default, Fine);

        Assert.Equal(LibraryChoiceResult.UsedChosen, result);
        Assert.Equal(@"E:\Softwares\IslamicScreensaver\cache", settings.CacheDirectory);
    }

    [Fact]
    public void AnExistingLibraryIsNeverRepointed()
    {
        // The user upgrading with 1.3 GB on E:\ — pointing the setting elsewhere would orphan it.
        var settings = new AppSettings { CacheDirectory = @"E:\Softwares\IslamicScreensaver\cache" };

        var result = LibraryChoice.Apply(settings, settingsExisted: true, @"D:\Elsewhere", Default, Fine);

        Assert.Equal(LibraryChoiceResult.KeptExisting, result);
        Assert.Equal(@"E:\Softwares\IslamicScreensaver\cache", settings.CacheDirectory);
    }

    [Fact]
    public void ExistingSettingsWithTheDefaultLibraryAreLeftAloneToo()
    {
        // Uninstalled without removing data, then reinstalled: the downloads are in the default place.
        var settings = new AppSettings();

        var result = LibraryChoice.Apply(settings, settingsExisted: true, @"D:\Elsewhere", Default, Fine);

        Assert.Equal(LibraryChoiceResult.KeptExisting, result);
        Assert.Null(settings.CacheDirectory);
    }

    [Theory]
    [InlineData(@"C:\Users\someone\AppData\Local")]                         // the default's own parent
    [InlineData(@"C:\Users\someone\AppData\Local\IslamicScreensaver")]
    [InlineData(null)]
    [InlineData("")]
    public void ChoosingTheDefaultRecordsNothing(string? chosen)
    {
        // Left as null, the library follows the default even if Windows moves the profile.
        var settings = new AppSettings();

        Assert.Equal(LibraryChoiceResult.UsedDefault, LibraryChoice.Apply(settings, false, chosen, Default, Fine));
        Assert.Null(settings.CacheDirectory);
    }

    [Fact]
    public void AFolderThatFailsValidationLeavesTheDefault()
    {
        var settings = new AppSettings();

        var result = LibraryChoice.Apply(settings, false, @"Z:\NotThere", Default, _ => "That drive is not available.");

        Assert.Equal(LibraryChoiceResult.Refused, result);
        Assert.Null(settings.CacheDirectory);
    }

    [Fact]
    public void ValidationSeesTheFinalFolder_NotWhatWasPicked()
    {
        string? checkedPath = null;
        LibraryChoice.Apply(new AppSettings(), false, @"E:\Softwares", Default, p => { checkedPath = p; return null; });

        Assert.Equal(@"E:\Softwares\IslamicScreensaver\cache", checkedPath);
    }

    [Theory]
    [InlineData(new[] { "/library", @"E:\My Stuff" }, @"E:\My Stuff")]    // a space survives when quoted as one argument
    [InlineData(new[] { "/library" }, null)]
    public void SetupCanPassTheFolder(string[] args, string? expected)
    {
        var parsed = ScreensaverArgs.Parse(args);

        Assert.Equal(ScreensaverMode.Library, parsed.Mode);
        Assert.Equal(expected, parsed.Value);
    }
}
