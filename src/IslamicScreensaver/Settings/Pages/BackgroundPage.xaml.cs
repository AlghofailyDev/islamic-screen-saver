using System.IO;
using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Rendering;
using Microsoft.Win32;

namespace IslamicScreensaver.Settings.Pages;

public partial class BackgroundPage : UserControl
{
    private readonly SettingsContext _ctx;

    internal BackgroundPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        DawnPreview.Load(new BackgroundSettings { Kind = BackgroundKind.Bundled, BundledName = BundledBackgrounds.Dawn });
        NightPreview.Load(new BackgroundSettings { Kind = BackgroundKind.Bundled, BundledName = BundledBackgrounds.Night });

        var bg = _ctx.Settings.Background;
        Mirror.IsChecked = _ctx.Settings.MirrorOnAllMonitors;
        if (bg.Kind == BackgroundKind.File && !string.IsNullOrEmpty(bg.FilePath))
        {
            FileThumb.IsChecked = true;
            ShowFile(bg.FilePath);
        }
        else if (string.Equals(bg.BundledName, BundledBackgrounds.Night, StringComparison.OrdinalIgnoreCase))
            NightThumb.IsChecked = true;
        else
            DawnThumb.IsChecked = true;
    }

    private void Bundled_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.Background.Kind = BackgroundKind.Bundled;
        _ctx.Settings.Background.BundledName = ReferenceEquals(sender, NightThumb) ? BundledBackgrounds.Night : BundledBackgrounds.Dawn;
        _ctx.Changed();
    }

    private void File_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ctx.Settings.Background.FilePath))
        {
            Choose_Click(sender, e);
            return;
        }
        _ctx.Settings.Background.Kind = BackgroundKind.File;
        _ctx.Changed();
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("dialog.chooseBackground"),
            Filter = "Images and videos|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.mp4;*.mov;*.wmv;*.mkv;*.m4v|Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Videos|*.mp4;*.mov;*.wmv;*.mkv;*.m4v|All files|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };
        if (dialog.ShowDialog() != true)
        {
            if (string.IsNullOrEmpty(_ctx.Settings.Background.FilePath))
                (_ctx.Settings.Background.BundledName == BundledBackgrounds.Night ? NightThumb : DawnThumb).IsChecked = true;
            return;
        }

        _ctx.Settings.Background.Kind = BackgroundKind.File;
        _ctx.Settings.Background.FilePath = dialog.FileName;
        _ctx.Changed();
        FileThumb.IsChecked = true;
        ShowFile(dialog.FileName);
    }

    private void ShowFile(string path)
    {
        FilePath.Text = path;
        FileHint.Visibility = File.Exists(path) ? Visibility.Collapsed : Visibility.Visible;
        FileHint.Text = File.Exists(path) ? string.Empty : "File not found";
        FilePreview.Load(new BackgroundSettings { Kind = BackgroundKind.File, FilePath = path });
    }

    private void Mirror_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.MirrorOnAllMonitors = Mirror.IsChecked == true;
        _ctx.Changed();
    }
}
