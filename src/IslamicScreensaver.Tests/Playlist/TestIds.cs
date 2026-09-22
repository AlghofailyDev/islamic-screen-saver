using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Playlist;

/// <summary>Short-hand for Quran.com recitation ids, which most playlist tests are written against.</summary>
internal static class TestIds
{
    public static RecitationId Q(int number) => RecitationId.QuranCom(number);

    public static RecitationId M(int number) => RecitationId.Mp3Quran(number);
}
