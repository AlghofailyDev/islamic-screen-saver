using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IslamicScreensaver.Core.Content;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>One reciter: pick surahs, download them, delete them, listen.</summary>
public partial class ReciterDetailPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly Reciter _reciter;
    private readonly Action _onChanged;
    private readonly List<ReciterSurahTile> _tiles = [];
    private bool _bulkUpdate;

    internal ReciterDetailPage(SettingsContext ctx, Reciter reciter, Action onChanged)
    {
        _ctx = ctx;
        _reciter = reciter;
        _onChanged = onChanged;
        InitializeComponent();

        Title.Text = reciter.Name;
        Subtitle.Text = reciter.Style ?? "Recitation";
        Initials.Text = string.Concat(reciter.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

        foreach (var chapter in _ctx.Chapters)
            _tiles.Add(new ReciterSurahTile(chapter, OnTileChanged));
        Tiles.ItemsSource = _tiles;

        _ctx.Downloads.Changed += OnJobChanged;
        _ctx.Downloads.Failed += OnJobFailed;
        _ctx.Samples.PlayingChanged += _ => UpdateListen();
        Unloaded += (_, _) =>
        {
            _ctx.Downloads.Changed -= OnJobChanged;
            _ctx.Downloads.Failed -= OnJobFailed;
        };

        RefreshStates();
        OnJobChanged(JobKey, _ctx.Downloads.ProgressOf(JobKey));
        UpdateListen();
    }

    private string JobKey => $"reciter:{_reciter.Id}";

    private bool IsDownloading => _ctx.Downloads.IsBusy(JobKey);

    private void RefreshStates()
    {
        var cached = new HashSet<int>(_ctx.Cache.CachedSurahsFor(_reciter.Id));
        foreach (var tile in _tiles)
            tile.SetState(cached.Contains(tile.Number) ? SurahState.Downloaded : SurahState.Missing);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var downloaded = _tiles.Count(t => t.State == SurahState.Downloaded);
        var selected = _tiles.Where(t => t.IsSelected).ToList();
        var toDownload = selected.Count(t => t.State != SurahState.Downloaded);
        var toDelete = selected.Count(t => t.State == SurahState.Downloaded);

        CountText.Text = selected.Count == 0 ? string.Empty : $"{selected.Count} selected";

        if (IsDownloading)
            return;

        StatusText.Text = downloaded == 114 ? "All 114 surahs downloaded" : downloaded == 0 ? "Nothing downloaded yet" : $"{downloaded} of 114 surahs downloaded";
        StatusDetail.Text = selected.Count == 0
            ? "Select surahs below, or use Select all / Select missing."
            : toDownload == 0 ? "Everything selected is already downloaded." : $"{toDownload} selected surah{(toDownload == 1 ? "" : "s")} to download";

        DownloadButton.Content = toDownload > 0 ? $"Download {toDownload}" : "Download selected";
        DownloadButton.IsEnabled = toDownload > 0;
        DeleteButton.Content = toDelete > 0 ? $"Delete {toDelete}" : "Delete selected";
        DeleteButton.IsEnabled = toDelete > 0;
        Progress.Visibility = Visibility.Collapsed;
    }

    private void OnTileChanged()
    {
        if (!_bulkUpdate)
            UpdateSummary();
    }

    private void OnJobChanged(string key, JobProgress? progress)
    {
        if (key != JobKey)
            return;

        if (progress is null)
        {
            foreach (var tile in _tiles.Where(t => t.State == SurahState.Downloading))
                tile.SetState(SurahState.Missing);
            RefreshStates();
            _onChanged();
            return;
        }

        // Mark finished ones as we go so the grid fills in live.
        var cached = new HashSet<int>(_ctx.Cache.CachedSurahsFor(_reciter.Id));
        foreach (var tile in _tiles)
        {
            if (cached.Contains(tile.Number))
                tile.SetState(SurahState.Downloaded);
            else if (tile.Number == progress.CurrentSurah)
                tile.SetState(SurahState.Downloading);
            else if (tile.State == SurahState.Downloading)
                tile.SetState(SurahState.Missing);
        }

        Progress.Visibility = Visibility.Visible;
        Progress.Value = progress.Fraction;
        StatusText.Text = progress.CurrentItem is null ? "Finishing…" : $"Downloading {progress.CurrentItem}";
        StatusDetail.Text = $"{progress.Done} of {progress.Total} done";
        DownloadButton.Content = "Cancel";
        DownloadButton.IsEnabled = true;
        DeleteButton.IsEnabled = false;
    }

    private void OnJobFailed(string key, string message)
    {
        if (key == JobKey)
            MessageBox.Show(message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (IsDownloading)
        {
            _ctx.Downloads.CancelAll();
            return;
        }

        var surahs = _tiles.Where(t => t.IsSelected && t.State != SurahState.Downloaded).Select(t => t.Number).ToList();
        if (surahs.Count == 0)
            return;
        _ctx.Downloads.EnqueueReciter(_reciter.Id, surahs);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var surahs = _tiles.Where(t => t.IsSelected && t.State == SurahState.Downloaded).Select(t => t.Number).ToList();
        if (surahs.Count == 0)
            return;

        var answer = MessageBox.Show(
            $"Delete {surahs.Count} downloaded surah{(surahs.Count == 1 ? "" : "s")} by {_reciter.Name} from this PC?",
            "Delete downloads", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        _ctx.Samples.Stop();
        foreach (var surah in surahs)
            _ctx.Cache.DeleteSurahAudio(_reciter.Id, surah);

        SetAll(false);
        RefreshStates();
        _onChanged();
    }

    private void SetAll(bool value, Func<ReciterSurahTile, bool>? where = null)
    {
        _bulkUpdate = true;
        foreach (var tile in _tiles)
            tile.IsSelected = value && (where?.Invoke(tile) ?? true);
        _bulkUpdate = false;
        UpdateSummary();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void SelectMissing_Click(object sender, RoutedEventArgs e) => SetAll(true, t => t.State != SurahState.Downloaded);

    private void Clear_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        var query = Search.Text.Trim();
        SearchHint.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tile in _tiles)
            tile.IsVisible = tile.Matches(query);
    }

    private void Listen_Click(object sender, RoutedEventArgs e)
    {
        var cached = _ctx.Cache.CachedSurahsFor(_reciter.Id);
        if (cached.Count == 0)
        {
            MessageBox.Show("Download a surah first, then you can listen.", "Nothing downloaded yet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var surah = cached.OrderByDescending(s => s).First();
        _ctx.Samples.Toggle(JobKey, _ctx.Cache.AudioPath(_reciter.Id, surah), (float)_ctx.Settings.Volume);
    }

    private void UpdateListen() =>
        ListenGlyph.Text = _ctx.Samples.PlayingKey == JobKey ? "\uE71A" : "\uE768";

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Samples.Stop();
        _onChanged();
        _ctx.NavigateBack?.Invoke();
    }
}

internal enum SurahState
{
    Missing,
    Downloading,
    Downloaded,
}

internal sealed class ReciterSurahTile : ObservableObject
{
    private static readonly Brush DownloadedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x7A, 0x63)));
    private static readonly Brush MissingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0x9D, 0x9D)));
    private static readonly Brush DownloadingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00)));

    private readonly Action _changed;
    private readonly string _nameArabic;
    private bool _isSelected;
    private bool _isVisible = true;
    private SurahState _state;

    public ReciterSurahTile(Chapter chapter, Action changed)
    {
        _changed = changed;
        Number = chapter.Number;
        NameSimple = chapter.NameSimple;
        _nameArabic = chapter.NameArabic;
    }

    public int Number { get; }
    public string NameSimple { get; }
    public SurahState State => _state;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
                _changed();
        }
    }

    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    public string StateText => _state switch
    {
        SurahState.Downloaded => "Downloaded",
        SurahState.Downloading => "Downloading…",
        _ => "Not downloaded",
    };

    public string StateGlyph => _state switch
    {
        SurahState.Downloaded => "\uE73E",   // check mark
        SurahState.Downloading => "\uE896",  // download arrow
        _ => "",
    };

    public Brush StateBrush => _state switch
    {
        SurahState.Downloaded => DownloadedBrush,
        SurahState.Downloading => DownloadingBrush,
        _ => MissingBrush,
    };

    public void SetState(SurahState state)
    {
        if (_state == state)
            return;
        _state = state;
        Raise(nameof(State));
        Raise(nameof(StateText));
        Raise(nameof(StateGlyph));
        Raise(nameof(StateBrush));
    }

    public bool Matches(string query) =>
        query.Length == 0
        || NameSimple.Contains(query, StringComparison.OrdinalIgnoreCase)
        || _nameArabic.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Number.ToString() == query;

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
