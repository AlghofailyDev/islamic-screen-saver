using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Tests.Content;

public sealed class CacheMoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "IslamicScreensaverTests", Guid.NewGuid().ToString("N"));

    private string Source => Path.Combine(_root, "source");
    private string Destination => Path.Combine(_root, "destination");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void Seed()
    {
        Directory.CreateDirectory(Path.Combine(Source, "audio", "7"));
        File.WriteAllBytes(Path.Combine(Source, "audio", "7", "1.mp3"), new byte[2048]);
        File.WriteAllText(Path.Combine(Source, "audio", "7", "1.timings.json"), "[]");
        File.WriteAllText(Path.Combine(Source, "manifest.json"), "{}");
    }

    [Fact]
    public async Task Move_CopiesEverything_ThenRemovesSource()
    {
        Seed();
        var reports = new List<MoveProgress>();

        await CacheMover.MoveAsync(Source, Destination, new Progress<MoveProgress>(reports.Add));

        Assert.False(Directory.Exists(Source));
        Assert.True(File.Exists(Path.Combine(Destination, "manifest.json")));
        Assert.Equal(2048, new FileInfo(Path.Combine(Destination, "audio", "7", "1.mp3")).Length);
        Assert.Equal(1, reports[^1].Fraction);
    }

    [Fact]
    public async Task Move_ToTheSamePlace_DoesNothing()
    {
        Seed();
        await CacheMover.MoveAsync(Source, Source + Path.DirectorySeparatorChar);
        Assert.True(File.Exists(Path.Combine(Source, "manifest.json")));
    }

    [Fact]
    public async Task Move_FromAMissingFolder_DoesNothing()
    {
        await CacheMover.MoveAsync(Path.Combine(_root, "nope"), Destination);
        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public void Validate_AcceptsAWritableFolder()
    {
        Seed();
        Assert.Null(CacheMover.Validate(Source, Destination));
    }

    [Fact]
    public void Validate_RejectsBlank_AndSubfolderOfSource()
    {
        Seed();
        Assert.NotNull(CacheMover.Validate(Source, "   "));
        Assert.NotNull(CacheMover.Validate(Source, Path.Combine(Source, "inner")));
    }

    [Fact]
    public void Validate_RejectsTheApplicationFolder()
    {
        Seed();
        Assert.NotNull(CacheMover.Validate(Source, Path.Combine(AppContext.BaseDirectory, "content")));
    }

    [Fact]
    public void SizeOf_CountsRecursively_AndToleratesMissing()
    {
        Seed();
        Assert.Equal(2048 + 2 + 2, CacheMover.SizeOf(Source));
        Assert.Equal(0, CacheMover.SizeOf(Path.Combine(_root, "nope")));
    }
}
