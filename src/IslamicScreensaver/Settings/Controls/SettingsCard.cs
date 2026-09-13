using System.Windows;
using System.Windows.Controls;

namespace IslamicScreensaver.Settings.Controls;

/// <summary>A Fluent "settings card": header and description on the left, the control on the right. Template lives in Theme.xaml.</summary>
public sealed class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(null));

    static SettingsCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingsCard), new FrameworkPropertyMetadata(typeof(SettingsCard)));
    }

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
}
