using System.Net;
using System.Text.RegularExpressions;

namespace IslamicScreensaver.Core.Content;

/// <summary>
/// Translation text from the API carries footnote markers (<c>&lt;sup foot_note=123&gt;1&lt;/sup&gt;</c>)
/// and the odd HTML entity. On screen we want plain prose.
/// </summary>
public static partial class TranslationText
{
    public static string Clean(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var text = FootnoteTag().Replace(raw, string.Empty);
        text = AnyTag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"<sup\b[^>]*>.*?</sup>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FootnoteTag();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
