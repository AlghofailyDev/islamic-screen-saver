using System.Text.Json;

namespace IslamicScreensaver.CatalogBuilder;

/// <summary>
/// Reading two hand-maintained public APIs. Fields go missing and turn null without warning, and
/// <c>TryGetProperty</c>/<c>TryGetInt32</c> still throw when the token is not the right kind at all —
/// so nothing here throws, and a malformed row is simply one row we do not publish.
/// </summary>
internal static class Json
{
    public static int? Int(this JsonElement element, string property) =>
        element.Property(property) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number)
            ? number
            : null;

    public static string? Text(this JsonElement element, string property) =>
        element.Property(property) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    public static IEnumerable<JsonElement> Items(this JsonElement element, string? property = null)
    {
        var array = property is null ? element : element.Property(property) ?? default;
        return array.ValueKind == JsonValueKind.Array ? array.EnumerateArray() : [];
    }

    public static IEnumerable<JsonElement> Items(this JsonElement? element, string? property = null) =>
        element is { } root ? root.Items(property) : [];

    private static JsonElement? Property(this JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value
            : null;
}
