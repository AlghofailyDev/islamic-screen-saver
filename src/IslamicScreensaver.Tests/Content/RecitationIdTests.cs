using System.Text.Json;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public class RecitationIdTests
{
    [Theory]
    [InlineData("qurancom:7", RecitationSource.QuranCom, 7)]
    [InlineData("mp3quran:1", RecitationSource.Mp3Quran, 1)]
    [InlineData("QuranCom:12", RecitationSource.QuranCom, 12)]
    public void Parses(string text, RecitationSource source, int number)
    {
        var id = RecitationId.Parse(text);
        Assert.Equal(source, id.Source);
        Assert.Equal(number, id.Number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("spotify:7")]
    [InlineData("qurancom:")]
    [InlineData("qurancom:0")]
    [InlineData("qurancom:-3")]
    [InlineData("qurancom:7:1")]
    public void RejectsNonsense(string text)
    {
        Assert.False(RecitationId.TryParse(text, out _));
        Assert.Throws<FormatException>(() => RecitationId.Parse(text));
    }

    [Fact]
    public void RoundTripsAsAString()
    {
        Assert.Equal("qurancom:7", RecitationId.QuranCom(7).ToString());
        Assert.Equal(RecitationId.QuranCom(7), RecitationId.Parse("qurancom:7"));
    }

    [Fact]
    public void TheSameNumberFromDifferentSourcesIsADifferentRecitation()
    {
        Assert.NotEqual(RecitationId.QuranCom(1), RecitationId.Mp3Quran(1));
    }

    [Fact]
    public void SerialisesAsAPlainStringAndAsADictionaryKey()
    {
        var id = RecitationId.Mp3Quran(42);
        Assert.Equal("\"mp3quran:42\"", JsonSerializer.Serialize(id));
        Assert.Equal(id, JsonSerializer.Deserialize<RecitationId>("\"mp3quran:42\""));

        var map = new Dictionary<RecitationId, int> { [id] = 5 };
        var json = JsonSerializer.Serialize(map);
        Assert.Equal("{\"mp3quran:42\":5}", json);
        Assert.Equal(5, JsonSerializer.Deserialize<Dictionary<RecitationId, int>>(json)![id]);
    }

    [Fact]
    public void FolderNameIsUsableOnWindows()
    {
        // A colon would be rejected by the file system, so the cache nests by source instead.
        Assert.DoesNotContain(':', RecitationId.QuranCom(7).FolderName);
        Assert.Equal("qurancom", RecitationId.QuranCom(7).FolderName);
        Assert.Equal("mp3quran", RecitationId.Mp3Quran(7).FolderName);
    }
}
