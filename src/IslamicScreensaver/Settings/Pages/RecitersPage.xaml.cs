using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Settings.Pages;

public partial class RecitersPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly List<ReciterRow> _rows = [];
    private bool _loading;

    internal RecitersPage(SettingsContext ctx)
    {
        _ctx = ctx;
        InitializeComponent();

        _ctx.CatalogChanged += Rebuild;
        _ctx.SettingsChanged += UpdateScope;
        _ctx.Downloads.Changed += (key, progress) =>
        {
            foreach (var row in _rows.Where(r => r.JobKey == key))
                row.OnJob(progress);
            UpdateStorage();
        };
        _ctx.Downloads.Failed += (key, message) =>
        {
            if (key.StartsWith("reciter:"))
                MessageBox.Show(message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        _ctx.Samples.PlayingChanged += _ =>
        {
            foreach (var row in _rows)
                row.RefreshPlaying();
        };

        _loading = true;
        Rotate.IsChecked = _ctx.Settings.RotateReciters;
        Volume.Value = _ctx.Settings.Volume * 100;
        SyncOffset.Value = _ctx.Settings.SyncOffsetMs;
        _loading = false;
        VolumeValue.Text = $"{Volume.Value:0}%";
        SyncOffsetValue.Text = $"{SyncOffset.Value:+0;-0;0} ms";

        Rebuild();
    }

    private void Rebuild()
    {
        _rows.Clear();
        var selected = _ctx.Settings.ReciterIds;
        foreach (var reciter in _ctx.Reciters)
            _rows.Add(new ReciterRow(_ctx, reciter, selected.Contains(reciter.Id), OnSelectionChanged));

        // Selected first, then the rest alphabetically — the ones in use are what you look for.
        List.ItemsSource = _rows.OrderByDescending(r => r.IsSelected).ThenBy(r => r.Name).ToList();
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateScope();
        UpdateStorage();
    }

    private void OnSelectionChanged()
    {
        _ctx.Settings.ReciterIds = _rows.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        _ctx.Changed();
    }

    private void UpdateScope()
    {
        var count = _ctx.EffectiveSurahs.Count;
        ScopeText.Text = count == 114 ? "Downloads cover all 114 surahs" : $"Downloads cover the {count} selected surahs";
        foreach (var row in _rows)
            row.RefreshStatus();
    }

    private void UpdateStorage()
    {
        try
        {
            var dir = new DirectoryInfo(_ctx.Cache.RootDirectory);
            var bytes = dir.Exists ? dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            StorageText.Text = $"{bytes / 1024.0 / 1024.0:0.0} MB on disk  ·  {_ctx.Cache.RootDirectory}";
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
            MessageBox.Show("Could not reach Quran.com. Check your connection and try again.", "Refresh failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        VolumeValue.Text = $"{Volume.Value:0}%";
        if (_loading) return;
        _ctx.Settings.Volume = Volume.Value / 100;
        _ctx.Changed();
    }

    private void SyncOffset_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SyncOffsetValue.Text = $"{SyncOffset.Value:+0;-0;0} ms";
        if (_loading) return;
        _ctx.Settings.SyncOffsetMs = (int)SyncOffset.Value;
        _ctx.Changed();
    }
}

/// <summary>One reciter in the list: selection, download state and a listen button.</summary>
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
    private string _statusText = string.Empty;
    private bool _isComplete;
    private bool _isDownloading;
    private double _progress;
    private string _downloadLabel = "Download";

    public ReciterRow(SettingsContext ctx, Reciter reciter, bool isSelected, Action selectionChanged)
    {
        _ctx = ctx;
        _selectionChanged = selectionChanged;
        Id = reciter.Id;
        Name = reciter.Name;
        Subtitle = reciter.Style ?? "Recitation";
        _isSelected = isSelected;
        JobKey = $"reciter:{reciter.Id}";

        var palette = AvatarPalette[Math.Abs(reciter.Id) % AvatarPalette.Length];
        AvatarBrush = (Brush)new BrushConverter().ConvertFromString(palette.Bg)!;
        AvatarForeground = (Brush)new BrushConverter().ConvertFromString(palette.Fg)!;
        Initials = string.Concat(reciter.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

        DownloadCommand = new RelayCommand(Download);
        PlayCommand = new RelayCommand(Play);

        OnJob(ctx.Downloads.ProgressOf(JobKey));
        RefreshStatus();
    }

    public int Id { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public string Initials { get; }
    public Brush AvatarBrush { get; }
    public Brush AvatarForeground { get; }
    public string JobKey { get; }
    public RelayCommand DownloadCommand { get; }
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

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public bool IsComplete { get => _isComplete; private set => Set(ref _isComplete, value); }
    public bool IsDownloading { get => _isDownloading; private set => Set(ref _isDownloading, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }
    public string DownloadLabel { get => _downloadLabel; private set => Set(ref _downloadLabel, value); }
    public bool ShowDownload => !IsComplete || IsDownloading;
    public string PlayGlyph => _ctx.Samples.PlayingKey == JobKey ? "\uE71A" : "\uE768"; // Stop / Play (Segoe Fluent Icons)

    public void RefreshStatus()
    {
        if (IsDownloading)
            return;

        var wanted = _ctx.EffectiveSurahs;
        var have = _ctx.Cache.CachedSurahsFor(Id);
        var missing = wanted.Count(s => !have.Contains(s));

        IsComplete = missing == 0;
        StatusText = missing == 0
            ? (wanted.Count == 114 ? "All 114 surahs" : $"All {wanted.Count} selected surahs")
            : have.Count == 0 ? "Not downloaded" : $"{wanted.Count - missing} of {wanted.Count} surahs";
        DownloadLabel = missing == 0 ? "Downloaded" : have.Count == 0 ? "Download" : $"Download {missing} more";
        Raise(nameof(ShowDownload));
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
        StatusText = progress.CurrentItem is null ? "Finishing…" : $"Downloading {progress.CurrentItem}  ·  {progress.Done} of {progress.Total}";
        DownloadLabel = "Cancel";
        Raise(nameof(ShowDownload));
    }

    public void RefreshPlaying() => Raise(nameof(PlayGlyph));

    private void Download()
    {
        if (IsDownloading)
        {
            _ctx.Downloads.CancelAll();
            return;
        }
        _ctx.Downloads.EnqueueReciter(Id, _ctx.EffectiveSurahs);
    }

    private void Play()
    {
        var cached = _ctx.Cache.CachedSurahsFor(Id);
        if (cached.Count == 0)
        {
            MessageBox.Show("Download something from this reciter first, then you can listen.", "Nothing cached yet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // Prefer a short surah so the sample is representative but brief.
        var surah = cached.OrderByDescending(s => s).First();
        _ctx.Samples.Toggle(JobKey, _ctx.Cache.AudioPath(Id, surah), (float)_ctx.Settings.Volume);
    }
}
