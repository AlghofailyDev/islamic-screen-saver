using System.Text.Json;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public class ModelsTests
{
    [Theory]
    [InlineData("1:1", 1, 1)]
    [InlineData("2:255", 2, 255)]
    [InlineData("114:6", 114, 6)]
    public void VerseKey_Parses(string text, int surah, int ayah)
    {
        Assert.Equal(new VerseKey(surah, ayah), VerseKey.Parse(text));
        Assert.Equal(text, VerseKey.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("0:1")]
    [InlineData("115:1")]
    [InlineData("2:0")]
    [InlineData("a:b")]
    [InlineData("1:2:3")]
    public void VerseKey_RejectsInvalid(string text)
    {
        Assert.False(VerseKey.TryParse(text, out _));
        Assert.Throws<FormatException>(() => VerseKey.Parse(text));
    }

    [Fact]
    public void VerseKey_OrdersBySurahThenAyah()
    {
        var keys = new[] { new VerseKey(2, 1), new VerseKey(1, 7), new VerseKey(1, 2) };
        Assert.Equal([new VerseKey(1, 2), new VerseKey(1, 7), new VerseKey(2, 1)], keys.Order());
    }

    [Fact]
    public void VerseKey_RoundTripsThroughJson()
    {
        var timing = new VerseTiming(new VerseKey(2, 255), 10, 20, [new WordSegment(1, 10, 15)]);
        var json = JsonSerializer.Serialize(timing);
        var back = JsonSerializer.Deserialize<VerseTiming>(json);
        Assert.Equal(timing, back with { Words = timing.Words });
        Assert.Equal(timing.Words[0], back!.Words[0]);
    }

    [Theory]
    [InlineData("Plain.", "Plain.")]
    [InlineData("Say, \"He is One,<sup foot_note=1>1</sup>\"", "Say, \"He is One,\"")]
    [InlineData("A &amp; B", "A & B")]
    [InlineData("  spaced   out  ", "spaced out")]
    [InlineData("<i>tag</i> kept text", "tag kept text")]
    [InlineData("", "")]
    public void TranslationText_Clean(string raw, string expected)
    {
        Assert.Equal(expected, TranslationText.Clean(raw));
    }
}
