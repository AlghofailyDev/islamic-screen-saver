using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Localisation;

public class StringsTests : IDisposable
{
    public void Dispose() => Strings.Use(AppLanguage.English);

    [Fact]
    public void EverythingIsTranslated()
    {
        // A missing Arabic string would otherwise show as English inside an Arabic UI.
        Assert.Empty(Strings.UntranslatedKeys());
    }

    [Fact]
    public void ProperNounsAreStillInTheTable()
    {
        // Exempt from the translation check, but not exempt from having a home: a source name on a
        // recitation row must come from a key like every other string on screen.
        Strings.Use(AppLanguage.Arabic);
        Assert.Equal("MP3 Quran", Strings.Get("recitation.source.mp3quran"));
    }

    [Fact]
    public void SwitchingLanguage_ChangesTheText()
    {
        Strings.Use(AppLanguage.English);
        Assert.Equal("Reciters", Strings.Get("nav.reciters"));
        Assert.False(Strings.IsArabic);

        Strings.Use(AppLanguage.Arabic);
        Assert.Equal("القرّاء", Strings.Get("nav.reciters"));
        Assert.True(Strings.IsArabic);
    }

    [Fact]
    public void Placeholders_AreFilledInBothLanguages()
    {
        Strings.Use(AppLanguage.English);
        Assert.Equal("Verse 3 of 7", Strings.Get("screensaver.verseOf", 3, 7));

        Strings.Use(AppLanguage.Arabic);
        Assert.Equal("الآية 3 من 7", Strings.Get("screensaver.verseOf", 3, 7));
    }

    [Theory]
    [InlineData("No! Indeed, the record of the righteous is in ʿilliyyūn.", false)]
    [InlineData("كَلَّآ إِنَّ كِتَـٰبَ ٱلْأَبْرَارِ", true)]
    [InlineData("AbdulBaset AbdulSamad (Murattal)", false)]
    [InlineData("مشاري راشد العفاسي", true)]
    public void TextDirection_FollowsTheContentNotTheUi(string text, bool expected)
    {
        Strings.Use(AppLanguage.Arabic);
        Assert.Equal(expected, Strings.IsRightToLeftText(text));
        Strings.Use(AppLanguage.English);
    }

    [Fact]
    public void TextWithoutLetters_FollowsTheUiLanguage()
    {
        Strings.Use(AppLanguage.Arabic);
        Assert.True(Strings.IsRightToLeftText("123"));
        Strings.Use(AppLanguage.English);
        Assert.False(Strings.IsRightToLeftText("123"));
    }

    [Fact]
    public void AnUnknownKey_ReturnsItself_SoItIsObviousOnScreen()
    {
        Assert.Equal("nope.missing", Strings.Get("nope.missing"));
    }

    [Fact]
    public void ChangingLanguage_RaisesChangedOnce()
    {
        Strings.Use(AppLanguage.English);
        var raised = 0;
        void Handler() => raised++;

        Strings.Changed += Handler;
        try
        {
            Strings.Use(AppLanguage.Arabic);
            Strings.Use(AppLanguage.Arabic);   // already Arabic: no event
            Assert.Equal(1, raised);
        }
        finally
        {
            Strings.Changed -= Handler;
        }
    }
}
