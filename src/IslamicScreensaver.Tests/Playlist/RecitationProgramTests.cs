using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;

namespace IslamicScreensaver.Tests.Playlist;

using static TestIds;

public class RecitationProgramTests
{
    private static readonly List<PlaylistItem> Playlist = [new(Q(7), 2), new(Q(7), 9), new(Q(7), 1), new(Q(7), 112)];

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
    public void ARecordingThatOpensWithTheBasmalahDoesNotGetASecondOne()
    {
        // Nearly every mp3quran.net recording recites the basmalah itself. Adding ours in front of
        // it would have the listener hear it twice.
        var playlist = new List<PlaylistItem> { new(M(1), 2), new(M(1), 112) };

        var steps = RecitationProgram.Build(playlist, startIndex: 0, startsMidSurah: false, audioHasBasmalah: _ => true);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 2),
            (RecitationPart.Surah, 2),
            (RecitationPart.Surah, 112),
        ], Parts(steps));
    }

    [Fact]
    public void TheAnswerIsPerRecording_SoAMixedPlaylistGetsBoth()
    {
        // Rotation can put a Quran.com recording and an mp3quran one in the same playlist.
        var playlist = new List<PlaylistItem> { new(Q(7), 2), new(M(1), 2) };

        var steps = RecitationProgram.Build(
            playlist, startIndex: 0, startsMidSurah: false,
            audioHasBasmalah: item => item.Recitation.Source == RecitationSource.Mp3Quran);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 2),
            (RecitationPart.Basmalah, 2), (RecitationPart.Surah, 2),   // Quran.com: we supply it
            (RecitationPart.Surah, 2),                                  // mp3quran: already there
        ], Parts(steps));
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
            (RecitationPart.Basmalah, 112),                             // waiting for the loop to come back round
        ], Parts(steps));
    }

    [Fact]
    public void TheSecondLapOpensTheResumedSurahWithItsBasmalah()
    {
        // The first pass joins a surah part-way, so its basmalah is left out. Every lap after that
        // starts the same surah from the beginning and must open it properly — the bug was that the
        // loop landed straight on the surah, so Al-Kawthar came round bare every time.
        List<PlaylistItem> playlist = [new(Q(2), 108), new(Q(2), 110), new(Q(2), 112)];

        var steps = RecitationProgram.Build(playlist, startIndex: 0, startsMidSurah: true);
        var loop = RecitationProgram.LoopStart(steps);

        // Two laps from the loop point, as a listener would hear them.
        var heard = Enumerable.Range(0, (steps.Count - loop) * 2)
            .Select(i => steps[loop + i % (steps.Count - loop)])
            .Select(s => (s.Part, s.Item.Surah))
            .ToArray();

        Assert.Equal(
        [
            (RecitationPart.Surah, 108),                                 // lap 1: resumed part-way
            (RecitationPart.Basmalah, 110), (RecitationPart.Surah, 110),
            (RecitationPart.Basmalah, 112), (RecitationPart.Surah, 112),
            (RecitationPart.Basmalah, 108),                              // and here it is, before the wrap
            (RecitationPart.Surah, 108),                                 // lap 2: no longer bare
            (RecitationPart.Basmalah, 110), (RecitationPart.Surah, 110),
            (RecitationPart.Basmalah, 112), (RecitationPart.Surah, 112),
            (RecitationPart.Basmalah, 108),
        ], heard);
    }

    [Fact]
    public void ASingleResumedSurahStillOpensProperlyOnEveryLap()
    {
        List<PlaylistItem> playlist = [new(Q(2), 112)];

        var steps = RecitationProgram.Build(playlist, startIndex: 0, startsMidSurah: true);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 112),
            (RecitationPart.Surah, 112),
            (RecitationPart.Basmalah, 112),
        ], Parts(steps));
        Assert.Equal(1, RecitationProgram.LoopStart(steps));
    }

    [Theory]
    [InlineData(1)]    // Al-Fatihah contains its own basmalah
    [InlineData(9)]    // at-Tawbah is recited without one
    public void NoTrailingBasmalahForASurahThatNeverTakesOne(int surah)
    {
        List<PlaylistItem> playlist = [new(Q(2), surah), new(Q(2), 112)];

        var steps = RecitationProgram.Build(playlist, startIndex: 0, startsMidSurah: true);

        Assert.DoesNotContain(Parts(steps), p => p == (RecitationPart.Basmalah, surah));
    }

    [Fact]
    public void NoTrailingBasmalahWhenTheRecordingCarriesItsOwn()
    {
        List<PlaylistItem> playlist = [new(M(236), 112), new(M(236), 110)];

        var steps = RecitationProgram.Build(
            playlist, startIndex: 0, startsMidSurah: true, audioHasBasmalah: _ => true);

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 112),
            (RecitationPart.Surah, 112),
            (RecitationPart.Surah, 110),
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
