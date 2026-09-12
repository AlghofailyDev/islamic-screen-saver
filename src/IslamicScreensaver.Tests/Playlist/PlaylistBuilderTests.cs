using IslamicScreensaver.Core.Playlist;
using IslamicScreensaver.Core.Settings;

namespace IslamicScreensaver.Tests.Playlist;

public class PlaylistBuilderTests
{
    private static Func<int, IReadOnlyCollection<int>> Cache(Dictionary<int, int[]> byReciter) =>
        id => byReciter.TryGetValue(id, out var s) ? s : [];

    [Fact]
    public void Sequential_SingleReciter_OnlyCachedSurahs_InOrder()
    {
        var settings = new AppSettings { ReciterIds = [7], SelectedSurahs = null };
        var cache = Cache(new() { [7] = [112, 1, 114] });

        var playlist = PlaylistBuilder.Build(settings, cache, [7]);

        Assert.Equal([new(7, 1), new(7, 112), new(7, 114)], playlist);
    }

    [Fact]
    public void Selection_FiltersToChosenSurahs()
    {
        var settings = new AppSettings { ReciterIds = [7], SelectedSurahs = [114, 2] };
        var cache = Cache(new() { [7] = [1, 2, 114] });

        var playlist = PlaylistBuilder.Build(settings, cache, [7]);

        Assert.Equal([new(7, 114), new(7, 2)], playlist);
    }

    [Fact]
    public void Rotation_TakesTurns_AndSkipsRecitersMissingASurah()
    {
        var settings = new AppSettings { ReciterIds = [7, 2], RotateReciters = true };
        var cache = Cache(new() { [7] = [1, 2, 3, 4], [2] = [1, 3, 4] });

        var playlist = PlaylistBuilder.Build(settings, cache, [7, 2]);

        // 1 → 7, 2 → 2's turn but missing so 7, 3 → 2, 4 → 7
        Assert.Equal([new(7, 1), new(7, 2), new(2, 3), new(7, 4)], playlist);
    }

    [Fact]
    public void RotationOff_UsesFirstReciterThatHasTheSurah()
    {
        var settings = new AppSettings { ReciterIds = [7, 2], RotateReciters = false };
        var cache = Cache(new() { [7] = [1, 3], [2] = [1, 2, 3] });

        var playlist = PlaylistBuilder.Build(settings, cache, [7, 2]);

        Assert.Equal([new(7, 1), new(2, 2), new(7, 3)], playlist);
    }

    [Fact]
    public void Shuffled_IsAPermutation_AndDeterministicForASeed()
    {
        var settings = new AppSettings { ReciterIds = [7], Order = PlayOrder.Shuffled };
        var cache = Cache(new() { [7] = Enumerable.Range(78, 37).ToArray() });

        var a = PlaylistBuilder.Build(settings, cache, [7], new Random(42));
        var b = PlaylistBuilder.Build(settings, cache, [7], new Random(42));

        Assert.Equal(a, b);
        Assert.Equal(Enumerable.Range(78, 37), a.Select(p => p.Surah).Order());
        Assert.NotEqual(Enumerable.Range(78, 37), a.Select(p => p.Surah));
    }

    [Fact]
    public void NoRecitersInSettings_FallsBackToWhateverIsCached()
    {
        var settings = new AppSettings { ReciterIds = [] };
        var cache = Cache(new() { [3] = [1] });

        var playlist = PlaylistBuilder.Build(settings, cache, [3]);

        Assert.Equal([new(3, 1)], playlist);
    }

    [Fact]
    public void NothingCached_YieldsEmptyPlaylist()
    {
        Assert.Empty(PlaylistBuilder.Build(new AppSettings(), Cache([]), []));
    }

    [Fact]
    public void StartIndex_ResumesAtLastSurah_OnlyWhenSequentialAndEnabled()
    {
        var playlist = new List<PlaylistItem> { new(7, 1), new(7, 2), new(7, 3) };
        var last = new PlaybackState { ReciterId = 7, Surah = 2 };

        Assert.Equal(1, PlaylistBuilder.StartIndex(playlist, new AppSettings(), last));
        Assert.Equal(0, PlaylistBuilder.StartIndex(playlist, new AppSettings { ResumeWhereLeftOff = false }, last));
        Assert.Equal(0, PlaylistBuilder.StartIndex(playlist, new AppSettings { Order = PlayOrder.Shuffled }, last));
        Assert.Equal(0, PlaylistBuilder.StartIndex(playlist, new AppSettings(), null));
        Assert.Equal(0, PlaylistBuilder.StartIndex(playlist, new AppSettings(), new PlaybackState { Surah = 99 }));
    }
}
