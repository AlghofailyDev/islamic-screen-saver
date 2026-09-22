using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings.Pages;

public partial class RecitersPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly List<ReciterRow> _rows = [];

    internal RecitersPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _ctx.CatalogChanged += Rebuild;
        _ctx.Downloads.Changed += (key, progress) =>
        {
            foreach (var row in _rows.Where(r => r.JobKey == key))
                row.OnJob(progress);
            UpdateStorage();
        };
        _ctx.Downloads.Failed += (key, message) =>
        {
            if (key.StartsWith("reciter:"))
                MessageBox.Show(message, Strings.Get("dialog.downloadFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        _ctx.Samples.PlayingChanged += _ =>
        {
            foreach (var row in _rows)
                row.RefreshPlaying();
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
        var selected = _ctx.SelectedQuranComIds;
        foreach (var reciter in _ctx.Reciters)
            _rows.Add(new ReciterRow(_ctx, reciter, selected.Contains(reciter.Id), OnSelectionChanged, Open));

        // Selected first, then the rest alphabetically — the ones in use are what you look for.
        List.ItemsSource = _rows.OrderByDescending(r => r.IsSelected).ThenBy(r => r.Name).ToList();
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
        // Controls inside the row (checkbox, buttons) handle their own clicks; anywhere else opens the reciter.
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

    private void OnSelectionChanged()
    {
        _ctx.SetSelectedQuranComIds(_rows.Where(r => r.IsSelected).Select(r => r.Id));
        _ctx.Changed();
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

/// <summary>One reciter in the list: playback selection, download state, listen and open buttons.</summary>
internal sealed class ReciterRow : ObservableObject
{
    private static readonly (string Bg, string Fg)[] AvatarPalette =
    [
        ("#E3ECE8", "#1F5C4B"), ("#ECE8E3", "#6B4A2B"), ("#E3E8EC", "#2B4A6B"),
        ("#E9E3EC", "#5A2B6B"), ("#ECE3E3", "#6B2B2B"), ("#E3ECEA", "#2B6B60"),
    ];

    private readonly SettingsContext _ctx;
    private readonly Action _selectionChanged;
    private bool _isSelected;
    private bool _isVisible = true;
    private string _statusText = string.Empty;
    private bool _isComplete;
    private bool _isDownloading;
    private double _progress;

    public ReciterRow(SettingsContext ctx, Reciter reciter, bool isSelected, Action selectionChanged, Action<ReciterRow> open)
    {
        _ctx = ctx;
        _selectionChanged = selectionChanged;
        Reciter = reciter;
        Id = reciter.Id;
        Name = reciter.Name;
        Subtitle = reciter.Style ?? Strings.Get("status.recitation");
        _isSelected = isSelected;
        JobKey = $"reciter:{reciter.Id}";

        var palette = AvatarPalette[Math.Abs(reciter.Id) % AvatarPalette.Length];
        AvatarBrush = (Brush)new BrushConverter().ConvertFromString(palette.Bg)!;
        AvatarForeground = (Brush)new BrushConverter().ConvertFromString(palette.Fg)!;
        Initials = string.Concat(reciter.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

        OpenCommand = new RelayCommand(() => open(this));
        PlayCommand = new RelayCommand(Play);

        OnJob(ctx.Downloads.ProgressOf(JobKey));
        RefreshStatus();
    }

    public Reciter Reciter { get; }
    public int Id { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public string Initials { get; }
    public Brush AvatarBrush { get; }
    public Brush AvatarForeground { get; }
    public string JobKey { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand PlayCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
                _selectionChanged();
        }
    }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public bool IsComplete { get => _isComplete; private set => Set(ref _isComplete, value); }
    public bool IsDownloading { get => _isDownloading; private set => Set(ref _isDownloading, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public string PlayGlyph => _ctx.Samples.PlayingKey == JobKey ? "\uE71A" : "\uE768"; // Stop / Play (Segoe Fluent Icons)

    public bool Matches(string query) =>
        query.Length == 0
        || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase);

    public void RefreshStatus()
    {
        if (IsDownloading)
            return;

        var have = _ctx.Cache.CachedSurahsFor(SettingsContext.Rec(Id)).Count;
        IsComplete = have == 114;
        StatusText = have == 0 ? Strings.Get("reciters.nothingDownloaded")
            : have == 114 ? Strings.Get("reciters.allSurahs")
            : Strings.Get("reciters.someDownloaded", have);
    }

    public void OnJob(JobProgress? progress)
    {
        if (progress is null)
        {
            IsDownloading = false;
            Progress = 0;
            RefreshStatus();
            return;
        }

        IsDownloading = true;
        Progress = progress.Fraction;
        StatusText = progress.CurrentItem is null
            ? Strings.Get("status.finishing")
            : Strings.Get("status.downloadingOf", progress.CurrentItem, progress.Done, progress.Total);
    }

    public void RefreshPlaying() => Raise(nameof(PlayGlyph));

    private void Play()
    {
        var cached = _ctx.Cache.CachedSurahsFor(SettingsContext.Rec(Id));
        if (cached.Count == 0)
        {
            MessageBox.Show(Strings.Get("dialog.downloadFirst"), Strings.Get("dialog.nothingDownloadedYet"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // Prefer a short surah so the sample is representative but brief.
        var surah = cached.OrderByDescending(s => s).First();
        _ctx.Samples.Toggle(JobKey, _ctx.Cache.AudioPath(SettingsContext.Rec(Id), surah), (float)_ctx.Settings.Volume);
    }
}
