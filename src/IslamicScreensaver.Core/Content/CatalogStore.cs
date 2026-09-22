using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Reads and writes <see cref="Catalog"/> documents. The builder tool and the settings app both go
/// through here, so what we publish and what we parse can never drift apart.
/// </summary>
public static class CatalogStore
{
    /// <summary>The file we publish and ship, under the cache root and in the app folder.</summary>
    public const string FileName = "catalog.json";

    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        // Arabic names are the point of this file; escaping them would quadruple its size.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(Catalog catalog) => JsonSerializer.Serialize(catalog, Options);

    /// <summary>Null when the text is not a catalog we understand — the caller falls back to what it has.</summary>
    public static Catalog? Parse(string json)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<Catalog>(json, Options);
            return catalog is null || catalog.Version > Catalog.CurrentVersion ? null : catalog;
        }
        catch (JsonException ex)
        {
            Log.Error("The catalog could not be parsed.", ex);
            return null;
        }
    }

    public static Catalog? Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not read {path}.", ex);
            return null;
        }
    }

    public static void Write(string path, Catalog catalog) => AtomicFile.WriteAllText(path, Serialize(catalog));
}
