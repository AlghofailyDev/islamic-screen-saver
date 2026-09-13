using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Pure JSON → model mapping for Quran.com API v4 responses, kept separate from HTTP so it can be
/// tested against captured payloads. Field shapes were confirmed against the live API on 2026-09-13.
/// </summary>
public static class QuranApiParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static IReadOnlyList<Reciter> ParseReciters(string json)
    {
        var dto = Deserialize<RecitationsDto>(json);
        return dto.Recitations
            .Select(r => new Reciter(r.Id, r.ReciterName ?? $"Reciter {r.Id}", NullIfEmpty(r.Style)))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<Chapter> ParseChapters(string json)
    {
        var dto = Deserialize<ChaptersDto>(json);
        return dto.Chapters
            .Select(c => new Chapter(
                c.Id,
                c.NameArabic ?? string.Empty,
                c.NameSimple ?? string.Empty,
                c.TranslatedName?.Name ?? string.Empty,
                c.VersesCount,
                c.BismillahPre))
            .OrderBy(c => c.Number)
            .ToList();
    }

    public static IReadOnlyList<TranslationResource> ParseTranslations(string json)
    {
        var dto = Deserialize<TranslationsListDto>(json);
        return dto.Translations
            .Select(t => new TranslationResource(t.Id, t.Name ?? string.Empty, t.AuthorName ?? string.Empty, t.LanguageName ?? string.Empty))
            .ToList();
    }

    public static SurahAudio ParseSurahAudio(string json, int reciterId, int surah)
    {
        var dto = Deserialize<ChapterRecitationDto>(json);
        var file = dto.AudioFile ?? throw new FormatException("Response has no audio_file.");
        if (string.IsNullOrEmpty(file.AudioUrl))
            throw new FormatException("audio_file has no audio_url.");

        var timings = (file.Timestamps ?? [])
            .Select(t => new VerseTiming(
                VerseKey.Parse(t.VerseKey ?? throw new FormatException("Timestamp without verse_key.")),
                t.TimestampFrom,
                t.TimestampTo,
                (t.Segments ?? [])
                    .Where(s => s.Length >= 3)
                    // Segments are [wordPosition, fromMs, toMs]; some reciters carry the times as floats.
                    .Select(s => new WordSegment((int)Math.Round(s[0]), (int)Math.Round(s[1]), (int)Math.Round(s[2])))
                    .ToList()))
            .OrderBy(t => t.Key)
            .ToList();

        long? size = file.FileSize is { } fs && fs > 0 ? (long)fs : null;
        return new SurahAudio(reciterId, surah, file.AudioUrl, size, timings);
    }

    public static IReadOnlyList<VerseText> ParseSurahText(string json)
    {
        var dto = Deserialize<VersesDto>(json);
        return dto.Verses
            .Select(v => new VerseText(
                VerseKey.Parse(v.VerseKey ?? throw new FormatException("Verse without verse_key.")),
                (v.TextUthmani ?? string.Empty).Trim(),
                (v.Words ?? [])
                    .Where(w => w.CharTypeName == "word")
                    .OrderBy(w => w.Position)
                    .Select(w => w.TextUthmani ?? string.Empty)
                    .ToList()))
            .OrderBy(v => v.Key)
            .ToList();
    }

    public static SurahTranslation ParseSurahTranslation(string json, int resourceId, int surah)
    {
        var dto = Deserialize<TranslationTextsDto>(json);
        var texts = dto.Translations.Select(t => TranslationText.Clean(t.Text ?? string.Empty)).ToList();
        return new SurahTranslation(resourceId, surah, texts);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new FormatException($"Empty {typeof(T).Name} response.");

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // --- DTOs: mirror the wire shape only; nothing outside this file should see them. ---

    private sealed class RecitationsDto
    {
        public List<RecitationDto> Recitations { get; set; } = [];
    }

    private sealed class RecitationDto
    {
        public int Id { get; set; }
        [JsonPropertyName("reciter_name")] public string? ReciterName { get; set; }
        public string? Style { get; set; }
    }

    private sealed class ChaptersDto
    {
        public List<ChapterDto> Chapters { get; set; } = [];
    }

    private sealed class ChapterDto
    {
        public int Id { get; set; }
        [JsonPropertyName("name_arabic")] public string? NameArabic { get; set; }
        [JsonPropertyName("name_simple")] public string? NameSimple { get; set; }
        [JsonPropertyName("verses_count")] public int VersesCount { get; set; }
        [JsonPropertyName("bismillah_pre")] public bool BismillahPre { get; set; }
        [JsonPropertyName("translated_name")] public TranslatedNameDto? TranslatedName { get; set; }
    }

    private sealed class TranslatedNameDto
    {
        public string? Name { get; set; }
    }

    private sealed class TranslationsListDto
    {
        public List<TranslationResourceDto> Translations { get; set; } = [];
    }

    private sealed class TranslationResourceDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("author_name")] public string? AuthorName { get; set; }
        [JsonPropertyName("language_name")] public string? LanguageName { get; set; }
    }

    private sealed class ChapterRecitationDto
    {
        [JsonPropertyName("audio_file")] public AudioFileDto? AudioFile { get; set; }
    }

    private sealed class AudioFileDto
    {
        [JsonPropertyName("audio_url")] public string? AudioUrl { get; set; }
        [JsonPropertyName("file_size")] public double? FileSize { get; set; }
        public List<TimestampDto>? Timestamps { get; set; }
    }

    private sealed class TimestampDto
    {
        [JsonPropertyName("verse_key")] public string? VerseKey { get; set; }
        [JsonPropertyName("timestamp_from")] public int TimestampFrom { get; set; }
        [JsonPropertyName("timestamp_to")] public int TimestampTo { get; set; }
        public List<double[]>? Segments { get; set; }
    }

    private sealed class VersesDto
    {
        public List<VerseDto> Verses { get; set; } = [];
    }

    private sealed class VerseDto
    {
        [JsonPropertyName("verse_key")] public string? VerseKey { get; set; }
        [JsonPropertyName("text_uthmani")] public string? TextUthmani { get; set; }
        public List<WordDto>? Words { get; set; }
    }

    private sealed class WordDto
    {
        public int Position { get; set; }
        [JsonPropertyName("char_type_name")] public string? CharTypeName { get; set; }
        [JsonPropertyName("text_uthmani")] public string? TextUthmani { get; set; }
    }

    private sealed class TranslationTextsDto
    {
        public List<TranslationTextDto> Translations { get; set; } = [];
    }

    private sealed class TranslationTextDto
    {
        public string? Text { get; set; }
    }
}
