using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
        InitializeComponent();
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
        Closing += (_, _) => { _context.Downloads.CancelAll(); _context.Flush(); };
        Loaded += async (_, _) =>
        {
            Nav.SelectedIndex = 0;
            // Silent background top-up: the cached catalog is used immediately, refreshed if it is a day old.
            await _context.RefreshCatalogIfStaleAsync();
        };
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigating || sender is not ListBox list || list.SelectedItem is not ListBoxItem item || item.Tag is not string key)
            return;

        // Two lists share one selection; clear the other without re-entering.
        _navigating = true;
        (ReferenceEquals(list, Nav) ? FooterNav : Nav).SelectedItem = null;
        _navigating = false;

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
        Nav.SelectedItem = Nav.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string?)i.Tag == tag);
    }

    private void RunScreensaver_Click(object sender, RoutedEventArgs e)
    {
        _context.Flush();
        if (Environment.ProcessPath is { } self)
            Process.Start(new ProcessStartInfo(self, "/s") { UseShellExecute = false });
    }
}
