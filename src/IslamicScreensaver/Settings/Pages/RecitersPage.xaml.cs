using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>
/// Every reciter we know of. One row per person, however many recordings they have — opening one
/// shows their recitations, because a reciter is not a recording: Husary alone has seven, across four
/// riwāyāt and both sources.
/// </summary>
public partial class RecitersPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly List<ReciterRow> _rows = [];

    internal RecitersPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _ctx.CatalogChanged += Rebuild;
        _ctx.Downloads.Changed += (_, _) =>
        {
            RefreshStatuses();
        };

        Rotate.IsChecked = _ctx.Settings.RotateReciters;
        Rebuild();
    }

    /// <summary>Called when returning from a reciter's page so download counts are current.</summary>
    public void RefreshStatuses()
    {
        foreach (var row in _rows)
            row.RefreshStatus();
        UpdateStorage();
    }

    private void Rebuild()
    {
        _rows.Clear();
        var selected = _ctx.Settings.Recitations.ToHashSet();
        foreach (var reciter in _ctx.Catalog.Reciters)
            _rows.Add(new ReciterRow(_ctx, reciter, selected));

        // The reciters you actually listen to first, then everyone else by name.
        List.ItemsSource = _rows.OrderByDescending(r => r.IsPlaying).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplySearch();
        UpdateStorage();
    }

    private void Open(ReciterRow row)
    {
        _ctx.Samples.Stop();
        _ctx.NavigateTo?.Invoke(new ReciterDetailPage(_ctx, row.Reciter, RefreshStatuses));
    }

    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        // Controls inside the row handle their own clicks; anywhere else opens the reciter.
        if (e.OriginalSource is DependencyObject source && HasInteractiveAncestor(source, (FrameworkElement)sender))
            return;
        if (sender is FrameworkElement { DataContext: ReciterRow row })
            Open(row);
    }

    private static bool HasInteractiveAncestor(DependencyObject node, FrameworkElement stopAt)
    {
        while (node is not null && !ReferenceEquals(node, stopAt))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase)
                return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void ApplySearch()
    {
        var query = Search.Text.Trim();
        SearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var visible = 0;
        foreach (var row in _rows)
        {
            row.IsVisible = row.Matches(query);
            if (row.IsVisible) visible++;
        }
        CountText.Text = _rows.Count == 0 ? string.Empty
            : query.Length == 0 ? Strings.Get("reciters.count", _rows.Count)
            : Strings.Get("surahs.someSelected", visible, _rows.Count);
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplySearch();

    private void UpdateStorage()
    {
        try
        {
            var dir = new DirectoryInfo(_ctx.Cache.RootDirectory);
            var bytes = dir.Exists ? dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            StorageText.Text = Strings.Get("status.mbOnDisk", (bytes / 1024.0 / 1024.0).ToString("0.0"), _ctx.Cache.RootDirectory);
        }
        catch (Exception ex)
        {
            Log.Error("Could not measure cache size.", ex);
        }
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.RotateReciters = Rotate.IsChecked == true;
        _ctx.Changed();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
            button.IsEnabled = false;
        var ok = await _ctx.RefreshCatalogAsync();
        if (sender is Button b2)
            b2.IsEnabled = true;
        if (!ok)
            MessageBox.Show(Strings.Get("dialog.cannotReach"), Strings.Get("dialog.refreshFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

/// <summary>One reciter in the list: how many recordings they have, and how much of them is on disk.</summary>
internal sealed class ReciterRow : ObservableObject
{
    private static readonly (string Bg, string Fg)[] AvatarPalette =
    [
        ("#E3ECE8", "#1F5C4B"), ("#ECE8E3", "#6B4A2B"), ("#E3E8EC", "#2B4A6B"),
        ("#E9E3EC", "#5A2B6B"), ("#ECE3E3", "#6B2B2B"), ("#E3ECEA", "#2B6B60"),
    ];

    private readonly SettingsContext _ctx;
    private bool _isVisible = true;
    private string _statusText = string.Empty;
    private bool _isPlaying;
    private bool _hasDownloads;

    public ReciterRow(SettingsContext ctx, ReciterEntry reciter, IReadOnlySet<RecitationId> selected)
    {
        _ctx = ctx;
        Reciter = reciter;
        Name = reciter.Name(Strings.IsArabic);
        Subtitle = reciter.Recitations.Count == 1
            ? reciter.Recitations[0].Describe(Strings.IsArabic)
            : Strings.Get("recitation.many", reciter.Recitations.Count);
        _isPlaying = reciter.Recitations.Any(r => selected.Contains(r.Id));

        var palette = AvatarPalette[Math.Abs(reciter.Id.GetHashCode()) % AvatarPalette.Length];
        AvatarBrush = (Brush)new BrushConverter().ConvertFromString(palette.Bg)!;
        AvatarForeground = (Brush)new BrushConverter().ConvertFromString(palette.Fg)!;
        Initials = Initial(Name);

        OpenCommand = new RelayCommand(() => ctx.NavigateTo?.Invoke(new ReciterDetailPage(ctx, reciter, () => { })));

        RefreshStatus();
    }

    /// <summary>Arabic names do not abbreviate to initials the way Latin ones do, so take one letter there.</summary>
    private static string Initial(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return "?";
        return Strings.IsRightToLeftText(name)
            ? words[0][..1]
            : string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }

    public ReciterEntry Reciter { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public string Initials { get; }
    public Brush AvatarBrush { get; }
    public Brush AvatarForeground { get; }
    public RelayCommand OpenCommand { get; }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public bool IsPlaying { get => _isPlaying; private set => Set(ref _isPlaying, value); }
    public bool HasDownloads { get => _hasDownloads; private set => Set(ref _hasDownloads, value); }

    public bool Matches(string query) => Reciter.Matches(query) || Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase);

    public void RefreshStatus()
    {
        var downloaded = Reciter.Recitations.Sum(r => _ctx.Cache.CachedSurahsFor(r.Id).Count);
        var available = Reciter.Recitations.Sum(r => r.AvailableSurahs.Count);

        HasDownloads = downloaded > 0;
        StatusText = downloaded == 0
            ? Strings.Get("reciters.nothingDownloaded")
            : Strings.Get("recitation.downloadedOf", downloaded, available);

        IsPlaying = Reciter.Recitations.Any(r => _ctx.Settings.Recitations.Contains(r.Id));
    }
}
