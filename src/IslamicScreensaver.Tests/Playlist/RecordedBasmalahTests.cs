using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Playlist;

namespace IslamicScreensaver.Tests.Playlist;

using static TestIds;

public sealed class RecordedBasmalahTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static IReadOnlyList<VerseTiming> From(int firstVerseStartMs) =>
        [new(new VerseKey(114, 1), firstVerseStartMs, firstVerseStartMs + 5000, [])];

    [Theory]
    // Real lead-ins from mp3quran:236 (Abdulrahman Al-Majed), measured 2026-09-23. Every surah but
    // al-Baqarah opens with the basmalah — which is why one probed surah cannot speak for a recording.
    [InlineData(4360, true)]   // surah 114
    [InlineData(5100, true)]   // surah 105
    [InlineData(3400, true)]   // surah 67
    [InlineData(860, false)]   // surah 2
    // And from the recordings that genuinely have none on some surahs.
    [InlineData(40, false)]    // mp3quran:21, surah 67
    [InlineData(100, false)]   // mp3quran:21, surah 78
    [InlineData(0, false)]     // every Quran.com recording
    [InlineData(14820, true)]  // mp3quran:59, surah 67 — a long opening is still an opening
    public void TheLeadInBeforeVerseOneDecidesIt(int firstVerseStartMs, bool expected)
    {
        Assert.Equal(expected, RecordedBasmalah.PrecedesFirstVerse(From(firstVerseStartMs)));
    }

    [Fact]
    public void NoTimingsMeansNoEvidence_SoTheSourcesHabitDecides()
    {
        // Nothing to read. Skipping a basmalah is a gentler mistake than playing two.
        Assert.False(RecordedBasmalah.PrecedesFirstVerse(null));
        Assert.False(RecordedBasmalah.PrecedesFirstVerse([]));

        var cache = new ContentCache(_root);
        Assert.True(RecordedBasmalah.IsInAudio(cache, M(236), 114));
        Assert.False(RecordedBasmalah.IsInAudio(cache, Q(7), 114));
    }

    [Fact]
    public async Task TheCachedTimingsAreWhatIsRead()
    {
        var cache = new ContentCache(_root);
        await cache.WriteTimingsAsync(M(236), 114, From(4360));
        await cache.WriteTimingsAsync(M(236), 2, From(860));

        Assert.True(RecordedBasmalah.IsInAudio(cache, M(236), 114));
        Assert.False(RecordedBasmalah.IsInAudio(cache, M(236), 2));
    }

    [Fact]
    public async Task OneRecordingGetsBothAnswers_SurahBySurah()
    {
        // The bug this replaced: a per-recording flag said "no basmalah" for mp3quran:236 because
        // surah 2 was probed, and every other surah then had the basmalah played twice.
        var cache = new ContentCache(_root);
        await cache.WriteTimingsAsync(M(236), 2, From(860));
        await cache.WriteTimingsAsync(M(236), 114, From(4360));

        List<PlaylistItem> playlist = [new(M(236), 2), new(M(236), 114)];
        var steps = RecitationProgram.Build(
            playlist, startIndex: 0, startsMidSurah: false,
            audioHasBasmalah: item => RecordedBasmalah.IsInAudio(cache, item.Recitation, item.Surah));

        Assert.Equal(
        [
            (RecitationPart.Istiadhah, 2),
            (RecitationPart.Basmalah, 2), (RecitationPart.Surah, 2),   // no lead-in: we supply one
            (RecitationPart.Surah, 114),                                // 4.4s of it already there
        ], steps.Select(s => (s.Part, s.Item.Surah)).ToArray());
    }

    [Fact]
    public void AlFatihahAndAtTawbahAreNeverAsked()
    {
        // Their answer is fixed, so the predicate — a file read per item — is not called for them.
        var asked = new List<int>();
        List<PlaylistItem> playlist = [new(M(1), 1), new(M(1), 9), new(M(1), 112)];

        RecitationProgram.Build(playlist, startIndex: 0, startsMidSurah: false, audioHasBasmalah: item =>
        {
            asked.Add(item.Surah);
            return false;
        });

        Assert.Equal([112], asked);
    }
}
