using IslamicScreensaver.Core.Playlist;

namespace IslamicScreensaver.Tests.Playlist;

public class RecitationProgramTests
{
    private static readonly List<PlaylistItem> Playlist = [new(7, 2), new(7, 9), new(7, 1), new(7, 112)];

    private static (RecitationPart Part, int Surah)[] Parts(IReadOnlyList<RecitationStep> steps) =>
        steps.Select(s => (s.Part, s.Item.Surah)).ToArray();

    [Theory]
    [InlineData(1, false)]    // Al-Fatihah opens with the basmalah as its first verse
    [InlineData(9, false)]    // At-Tawbah is recited without one
    [InlineData(2, true)]
    [InlineData(112, true)]
    [InlineData(114, true)]
    public void NeedsBasmalah(int surah, bool expected)
    {
        Assert.Equal(expected, RecitationProgram.NeedsBasmalah(surah));
    }

    [Fact]
    public void OpensWithIstiadhah_ThenBasmalahBeforeEachSurahThatNeedsOne()
    {
        var steps = RecitationProgram.Build(Playlist, startIndex: 0, startsMidSurah: false);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 2),
            (RecitationPart.Basmalah, 2), (RecitationPart.Surah, 2),
            (RecitationPart.Surah, 9),                                  // At-Tawbah: no basmalah
            (RecitationPart.Surah, 1),                                  // Al-Fatihah: already contains it
            (RecitationPart.Basmalah, 112), (RecitationPart.Surah, 112),
        ], Parts(steps));
    }

    [Fact]
    public void ResumingMidSurah_SkipsThatSurahsBasmalah_ButKeepsTheIstiadhah()
    {
        var steps = RecitationProgram.Build(Playlist, startIndex: 3, startsMidSurah: true);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 112),
            (RecitationPart.Surah, 112),                                // resumed: its basmalah has passed
            (RecitationPart.Basmalah, 2), (RecitationPart.Surah, 2),    // wraps round and behaves normally
            (RecitationPart.Surah, 9),
            (RecitationPart.Surah, 1),
        ], Parts(steps));
    }

    [Fact]
    public void StartingAtASurahBeginning_KeepsItsBasmalah()
    {
        var steps = RecitationProgram.Build(Playlist, startIndex: 3, startsMidSurah: false);

        Assert.Equal((RecitationPart.Istiadhah, 112), Parts(steps)[0]);
        Assert.Equal((RecitationPart.Basmalah, 112), Parts(steps)[1]);
        Assert.Equal((RecitationPart.Surah, 112), Parts(steps)[2]);
    }

    [Fact]
    public void LoopStart_SkipsTheIstiadhah_SoItOpensTheSessionOnly()
    {
        var steps = RecitationProgram.Build(Playlist, 0, false);

        Assert.Equal(RecitationPart.Istiadhah, steps[0].Part);
        Assert.Equal(1, RecitationProgram.LoopStart(steps));
    }

    [Fact]
    public void EmptyPlaylist_YieldsNothing()
    {
        Assert.Empty(RecitationProgram.Build([], 0, false));
        Assert.Equal(0, RecitationProgram.LoopStart([]));
    }

    [Fact]
    public void StepsCarryTheirPlaylistIndex_SoStateCanBeSaved()
    {
        var steps = RecitationProgram.Build(Playlist, startIndex: 2, startsMidSurah: false);

        Assert.Equal(2, steps[0].PlaylistIndex);          // istiadhah, before surah 1
        Assert.Equal(2, steps[1].PlaylistIndex);          // surah 1 (already opens with the basmalah)
        Assert.Equal(3, steps[2].PlaylistIndex);          // basmalah for 112
        Assert.Equal(3, steps[3].PlaylistIndex);
    }
}
