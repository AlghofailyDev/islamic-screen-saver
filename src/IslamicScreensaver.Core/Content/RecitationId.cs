using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>Where a recitation comes from. The prefix of every <see cref="RecitationId"/>.</summary>
public enum RecitationSource
{
    /// <summary>quran.com — word-level timings, all Hafs.</summary>
    QuranCom,

    /// <summary>mp3quran.net — many riwāyāt and styles, ayah-level timings.</summary>
    Mp3Quran,
}

/// <summary>
/// Identifies one recording set across sources, e.g. <c>qurancom:7</c> or <c>mp3quran:1</c>.
/// Ids from different sources collide numerically, so they are always qualified.
/// </summary>
[JsonConverter(typeof(RecitationIdConverter))]
public readonly record struct RecitationId(RecitationSource Source, int Number)
{
    public const string QuranComPrefix = "qurancom";
    public const string Mp3QuranPrefix = "mp3quran";

    public static RecitationId QuranCom(int number) => new(RecitationSource.QuranCom, number);

    public static RecitationId Mp3Quran(int number) => new(RecitationSource.Mp3Quran, number);

    public string Prefix => Source == RecitationSource.QuranCom ? QuranComPrefix : Mp3QuranPrefix;

    /// <summary>A folder name for this recitation's audio. Never contains a colon, which Windows forbids.</summary>
    public string FolderName => Prefix;

    public static RecitationId Parse(string text) =>
        TryParse(text, out var id) ? id : throw new FormatException($"'{text}' is not a recitation id.");

    public static bool TryParse(string? text, out RecitationId id)
    {
        id = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var number) || number <= 0)
            return false;

        var source = parts[0].ToLowerInvariant() switch
        {
            QuranComPrefix => RecitationSource.QuranCom,
            Mp3QuranPrefix => RecitationSource.Mp3Quran,
            _ => (RecitationSource?)null,
        };
        if (source is null)
            return false;

        id = new RecitationId(source.Value, number);
        return true;
    }

    public override string ToString() => $"{Prefix}:{Number}";
}

internal sealed class RecitationIdConverter : JsonConverter<RecitationId>
{
    public override RecitationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        RecitationId.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, RecitationId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    public override RecitationId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        RecitationId.Parse(reader.GetString() ?? string.Empty);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, RecitationId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.ToString());
}
