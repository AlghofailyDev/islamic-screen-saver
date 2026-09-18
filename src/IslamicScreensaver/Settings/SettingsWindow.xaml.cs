using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;
using IslamicScreensaver.Settings.Pages;

namespace IslamicScreensaver.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsContext _context = new();
    private readonly Dictionary<string, UserControl> _pages = new();
    private readonly Stack<object?> _detailStack = new();
    private bool _navigating;

    public SettingsWindow()
    {
        ApplyThemeAndLanguage();
        InitializeComponent();
        // ThemeMode drives WPF's own Fluent control styles (combo boxes, scroll bars, check boxes) and
        // only takes effect once the window is initialised, so it is applied after InitializeComponent.
        ApplyThemeMode();
        LocaliseShell();

        _context.NavigateTo = page =>
        {
            _detailStack.Push(PageHost.Content);
            PageHost.Content = page;
            PageScroll.ScrollToTop();
        };
        _context.NavigateBack = () =>
        {
            if (_detailStack.Count > 0)
                PageHost.Content = _detailStack.Pop();
            PageScroll.ScrollToTop();
        };
        // Appearance can change either of these; rebuild so every page picks them up.
        _context.ChromeChanged = () =>
        {
            ApplyThemeAndLanguage();
            ApplyThemeMode();
            LocaliseShell();
            RebuildPages();
        };

        Closing += (_, _) => { _context.Downloads.CancelAll(); _context.Flush(); };
        Loaded += async (_, _) =>
        {
            Nav.SelectedIndex = 0;
            // Silent background top-up: the cached catalog is used immediately, refreshed if it is a day old.
            await _context.RefreshCatalogIfStaleAsync();
        };
    }

    private AppTheme _appliedTheme = AppTheme.Light;

    private void ApplyThemeMode() =>
        ThemeMode = _appliedTheme == AppTheme.Dark ? ThemeMode.Dark : ThemeMode.Light;

    private void ApplyThemeAndLanguage()
    {
        Strings.Use(_context.Settings.Language);
        _appliedTheme = ThemePalette.Apply(_context.Settings.Theme);
        FlowDirection = Strings.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var resources = Application.Current.Resources;
        resources["OnText"] = Strings.Get("common.on");
        resources["OffText"] = Strings.Get("common.off");
        resources["UiFont"] = new System.Windows.Media.FontFamily(
            Strings.IsArabic ? "Segoe UI, Tahoma" : "Segoe UI Variable Text, Segoe UI");
    }

    /// <summary>Text that lives in this window rather than on a page.</summary>
    private void LocaliseShell()
    {
        Title = Strings.Get("app.name");
        AppName.Text = Strings.Get("app.name");
        RunButton.Content = Strings.Get("shell.run");

        foreach (var item in Nav.Items.Cast<ListBoxItem>().Concat(FooterNav.Items.Cast<ListBoxItem>()))
        {
            if (item.Tag is string tag && item.Content is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock label)
                label.Text = Strings.Get($"nav.{tag}");
        }
    }

    private void RebuildPages()
    {
        var current = _pages.FirstOrDefault(p => ReferenceEquals(p.Value, PageHost.Content)).Key;
        _pages.Clear();
        _detailStack.Clear();
        PageHost.Content = null;

        // Re-select the page that was open so the rebuild is invisible apart from the new look.
        var tag = current ?? (Nav.SelectedItem as ListBoxItem)?.Tag as string ?? "library";
        SelectNav(tag);
        if (PageHost.Content is null)
            Show(tag);
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigating || sender is not ListBox list || list.SelectedItem is not ListBoxItem item || item.Tag is not string key)
            return;

        // Two lists share one selection; clear the other without re-entering.
        _navigating = true;
        (ReferenceEquals(list, Nav) ? FooterNav : Nav).SelectedItem = null;
        _navigating = false;

        Show(key);
    }

    private void Show(string key)
    {
        _detailStack.Clear();
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                "library" => new LibraryPage(_context, () => SelectNav("reciters")),
                "reciters" => new RecitersPage(_context),
                "surahs" => new SurahsPage(_context),
                "audio" => new AudioPage(_context),
                "background" => new BackgroundPage(_context),
                "appearance" => new AppearancePage(_context),
                "translation" => new TranslationPage(_context),
                _ => new AboutPage(_context),
            };
            _pages[key] = page;
        }
        PageHost.Content = page;
    }

    /// <summary>Moves the nav selection, e.g. the Library's "Browse reciters" button.</summary>
    private void SelectNav(string tag)
    {
        var item = Nav.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string?)i.Tag == tag)
                   ?? FooterNav.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string?)i.Tag == tag);
        if (item is null)
            return;

        if (Nav.Items.Contains(item))
            Nav.SelectedItem = item;
        else
            FooterNav.SelectedItem = item;
    }

    private void RunScreensaver_Click(object sender, RoutedEventArgs e)
    {
        _context.Flush();
        if (Environment.ProcessPath is { } self)
            Process.Start(new ProcessStartInfo(self, "/s") { UseShellExecute = false });
    }
}
