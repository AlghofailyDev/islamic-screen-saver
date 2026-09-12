namespace IslamicScreensaver.Core.Content;

/// <summary>The slice of Quran.com's API v4 the app needs. Everything returned is already mapped to app models.</summary>
public interface IQuranApi
{
    Task<IReadOnlyList<Reciter>> GetRecitersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TranslationResource>> GetTranslationsAsync(CancellationToken ct = default);

    Task<SurahAudio> GetSurahAudioAsync(int reciterId, int surah, CancellationToken ct = default);

    Task<IReadOnlyList<VerseText>> GetSurahTextAsync(int surah, CancellationToken ct = default);

    Task<SurahTranslation> GetSurahTranslationAsync(int resourceId, int surah, CancellationToken ct = default);
}
