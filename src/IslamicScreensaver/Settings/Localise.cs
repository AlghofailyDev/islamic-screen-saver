using System.Windows.Markup;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings;

/// <summary>
/// XAML shorthand for a translated string: <c>Text="{s:Loc nav.reciters}"</c>. Resolved when the page
/// is built, which is enough — changing language rebuilds the pages.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
