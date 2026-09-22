using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Surah lists as "1,78-114" rather than a hundred-odd JSON numbers. The catalog holds one of these
/// per recitation, so the file stays small enough to read and diff by eye.
/// </summary>
public static class SurahRange
{
    /// <summary>"1,4,5,6,9" becomes "1,4-6,9". An empty list becomes "".</summary>
    public static string Format(IEnumerable<int> surahs)
    {
        var sorted = surahs.Where(s => s is >= 1 and <= 114).Distinct().Order().ToList();
        var text = new StringBuilder();
        for (var i = 0; i < sorted.Count;)
        {
            var start = i;
            while (i + 1 < sorted.Count && sorted[i + 1] == sorted[i] + 1)
                i++;

            if (text.Length > 0)
                text.Append(',');
            text.Append(sorted[start]);
            if (i > start)
                text.Append(i == start + 1 ? ',' : '-').Append(sorted[i]);
            i++;
        }
        return text.ToString();
    }

    /// <summary>The inverse. Anything unparsable is skipped rather than thrown over.</summary>
    public static IReadOnlyList<int> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var surahs = new SortedSet<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var dash = part.IndexOf('-');
            if (dash < 0)
            {
                if (int.TryParse(part, out var single))
                    Add(surahs, single, single);
                continue;
            }

            if (int.TryParse(part[..dash], out var from) && int.TryParse(part[(dash + 1)..], out var to))
                Add(surahs, from, to);
        }
        return [.. surahs];
    }

    private static void Add(SortedSet<int> into, int from, int to)
    {
        for (var surah = Math.Max(from, 1); surah <= Math.Min(to, 114); surah++)
            into.Add(surah);
    }
}

/// <summary>Reads and writes an <c>IReadOnlyList&lt;int&gt;</c> of surahs as a range string.</summary>
public sealed class SurahRangeConverter : JsonConverter<IReadOnlyList<int>>
{
    public override IReadOnlyList<int> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        // Tolerate a plain array, so a hand-edited catalog still loads.
        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize<List<int>>(ref reader, options) ?? [];
        return SurahRange.Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<int> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(SurahRange.Format(value));
}
