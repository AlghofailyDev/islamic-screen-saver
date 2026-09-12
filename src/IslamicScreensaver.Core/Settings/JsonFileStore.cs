using System.Text.Json;
using System.Text.Json.Serialization;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Core.Settings;

/// <summary>
/// Load-or-default / save-atomically for one JSON document. A missing or corrupt file yields a fresh
/// default rather than an exception, because the screensaver must start no matter what.
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Load<T>(string path) where T : class, new()
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex)
        {
            Log.Error($"Could not read {path}; using defaults.", ex);
        }
        return new T();
    }

    public static void Save<T>(string path, T value)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception ex)
        {
            Log.Error($"Could not write {path}.", ex);
        }
    }
}
