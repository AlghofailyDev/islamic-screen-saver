using IslamicScreensaver.Core.Screensaver;

namespace IslamicScreensaver.Tests.Screensaver;

public class ScreensaverArgsTests
{
    [Fact]
    public void NoArguments_OpensSettings()
    {
        var args = ScreensaverArgs.Parse([]);
        Assert.Equal(ScreensaverMode.Configure, args.Mode);
        Assert.Equal(0, args.ParentWindow);
    }

    [Theory]
    [InlineData("/s")]
    [InlineData("/S")]
    [InlineData("-s")]
    [InlineData("/s:1234")]
    public void SlashS_Runs(string arg)
    {
        Assert.Equal(ScreensaverMode.Run, ScreensaverArgs.Parse([arg]).Mode);
    }

    [Fact]
    public void SlashP_WithSeparateHandle_Previews()
    {
        var args = ScreensaverArgs.Parse(["/p", "65538"]);
        Assert.Equal(ScreensaverMode.Preview, args.Mode);
        Assert.Equal(65538, args.ParentWindow);
    }

    [Fact]
    public void SlashP_WithoutHandle_HasNoParent()
    {
        var args = ScreensaverArgs.Parse(["/p"]);
        Assert.Equal(ScreensaverMode.Preview, args.Mode);
        Assert.Equal(0, args.ParentWindow);
    }

    [Theory]
    [InlineData("/c:9876", 9876)]
    [InlineData("/C:9876", 9876)]
    [InlineData("/c", 0)]
    public void SlashC_Configures_WithInlineHandle(string arg, long expectedHandle)
    {
        var args = ScreensaverArgs.Parse([arg]);
        Assert.Equal(ScreensaverMode.Configure, args.Mode);
        Assert.Equal((nint)expectedHandle, args.ParentWindow);
    }

    [Fact]
    public void SlashC_WithSeparateHandle_Configures()
    {
        var args = ScreensaverArgs.Parse(["/c", "42"]);
        Assert.Equal(ScreensaverMode.Configure, args.Mode);
        Assert.Equal(42, args.ParentWindow);
    }

    [Theory]
    [InlineData("/a")]
    [InlineData("/x")]
    [InlineData("garbage")]
    public void UnknownFlags_FallBackToSettings(string arg)
    {
        Assert.Equal(ScreensaverMode.Configure, ScreensaverArgs.Parse([arg]).Mode);
    }

    [Fact]
    public void NonNumericHandle_IsIgnored()
    {
        Assert.Equal(0, ScreensaverArgs.Parse(["/p", "abc"]).ParentWindow);
    }
}
