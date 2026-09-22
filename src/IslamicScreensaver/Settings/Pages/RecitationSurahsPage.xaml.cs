using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>
/// One recording: pick surahs, download them, delete them, listen. Only the surahs this recording
/// actually has are shown — a gap in the source is something the user should never have to discover
/// by watching a download fail.
/// </summary>
public partial class RecitationSurahsPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly Recitation _recitation;
    private readonly ReciterEntry _reciter;
    private readonly Action _onChanged;
    private readonly List<ReciterSurahTile> _tiles = [];
    private bool _bulkUpdate;

    internal RecitationSurahsPage(SettingsContext ctx, ReciterEntry reciter, Recitation recitation, Action onChanged)
    {
        _ctx = ctx;
        _reciter = reciter;
        _recitation = recitation;
        _onChanged = onChanged;
        InitializeComponent();

        var name = reciter.Name(Strings.IsArabic);
        BackLabel.Text = name;
        Title.Text = name;
        Subtitle.Text = recitation.Describe(Strings.IsArabic);
        Initials.Text = name.Length > 0 ? name[..1] : "?";

        var available = recitation.AvailableSurahs.ToHashSet();
        foreach (var chapter in _ctx.Chapters.Where(c => available.Contains(c.Number)))
            _tiles.Add(new ReciterSurahTile(chapter, !recitation.IsTimed(chapter.Number), OnTileChanged));
        Tiles.ItemsSource = _tiles;

        _ctx.Downloads.Changed += OnJobChanged;
        _ctx.Downloads.Failed += OnJobFailed;
        _ctx.Downloads.Notice += OnJobNotice;
        _ctx.Samples.PlayingChanged += _ => UpdateListen();
        Unloaded += (_, _) =>
        {
            _ctx.Downloads.Changed -= OnJobChanged;
            _ctx.Downloads.Failed -= OnJobFailed;
            _ctx.Downloads.Notice -= OnJobNotice;
        };

        RefreshStates();
        OnJobChanged(JobKey, _ctx.Downloads.ProgressOf(JobKey));
        UpdateListen();
    }

    private RecitationId Id => _recitation.Id;

    private string JobKey => $"reciter:{Id}";

    private bool IsDownloading => _ctx.Downloads.IsBusy(JobKey);

    private void RefreshStates()
    {
        var cached = new HashSet<int>(_ctx.Cache.CachedSurahsFor(Id));
        var unavailable = new HashSet<int>(_ctx.Cache.UnavailableSurahsFor(Id));
        foreach (var tile in _tiles)
        {
            tile.SetState(cached.Contains(tile.Number) ? SurahState.Downloaded
                : unavailable.Contains(tile.Number) ? SurahState.Unavailable
                : SurahState.Missing);
            if (tile.State == SurahState.Unavailable)
                tile.IsSelected = false;
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var downloaded = _tiles.Count(t => t.State == SurahState.Downloaded);
        var unavailable = _tiles.Count(t => t.State == SurahState.Unavailable);
        var obtainable = _tiles.Count - unavailable;
        var selected = _tiles.Where(t => t.IsSelected).ToList();
        var toDownload = selected.Count(t => t.State == SurahState.Missing);
        var toDelete = selected.Count(t => t.State == SurahState.Downloaded);

        CountText.Text = selected.Count == 0 ? string.Empty : $"{selected.Count} selected";

        if (IsDownloading)
            return;

        StatusText.Text = downloaded == obtainable && obtainable > 0
            ? (unavailable == 0 ? Strings.Get("status.allDownloaded") : Strings.Get("status.allAvailableDownloaded", obtainable))
            : downloaded == 0 ? Strings.Get("status.nothingDownloadedYet")
            : Strings.Get("status.downloadedOf", downloaded, obtainable);
        StatusDetail.Text = selected.Count == 0
            ? unavailable > 0
                ? Strings.Get("status.notHosted", unavailable)
                : Strings.Get("status.selectBelow")
            : toDownload == 0 ? Strings.Get("status.alreadyDownloaded") : Strings.Get("status.toDownload", toDownload);

        DownloadButton.Content = toDownload > 0 ? Strings.Get("status.downloadN", toDownload) : Strings.Get("reciters.downloadSelected");
        DownloadButton.IsEnabled = toDownload > 0;
        DeleteButton.Content = toDelete > 0 ? Strings.Get("status.deleteN", toDelete) : Strings.Get("reciters.deleteSelected");
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
        var cached = new HashSet<int>(_ctx.Cache.CachedSurahsFor(Id));
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
        StatusText.Text = progress.CurrentItem is null ? Strings.Get("status.finishing") : Strings.Get("status.downloadingItem", progress.CurrentItem);
        StatusDetail.Text = Strings.Get("status.doneOf", progress.Done, progress.Total);
        DownloadButton.Content = Strings.Get("common.cancel");
        DownloadButton.IsEnabled = true;
        DeleteButton.IsEnabled = false;
    }

    private void OnJobFailed(string key, string message)
    {
        if (key == JobKey)
            MessageBox.Show(message, Strings.Get("dialog.downloadFailed"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnJobNotice(string key, string message)
    {
        if (key == JobKey)
            MessageBox.Show(message, Strings.Get("dialog.someSkipped"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (IsDownloading)
        {
            _ctx.Downloads.CancelAll();
            return;
        }

        var surahs = _tiles.Where(t => t.IsSelected && t.State == SurahState.Missing).Select(t => t.Number).ToList();
        if (surahs.Count == 0)
            return;

        // Al-Fatihah comes along when this recording needs it for a basmalah — its first verse is
        // the basmalah in the reciter's own voice. A recording that already opens with one does not.
        _ctx.Downloads.EnqueueReciter(_recitation, surahs, alsoAlFatihah: true);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var surahs = _tiles.Where(t => t.IsSelected && t.State == SurahState.Downloaded).Select(t => t.Number).ToList();
        if (surahs.Count == 0)
            return;

        var answer = MessageBox.Show(
            Strings.Get("dialog.deleteSelectedAsk", surahs.Count, _reciter.Name(Strings.IsArabic)),
            Strings.Get("dialog.deleteDownloads"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        _ctx.Samples.Stop();
        foreach (var surah in surahs)
            _ctx.Cache.DeleteSurahAudio(Id, surah);

        SetAll(false);
        RefreshStates();
        _onChanged();
    }

    private void SetAll(bool value, Func<ReciterSurahTile, bool>? where = null)
    {
        _bulkUpdate = true;
        foreach (var tile in _tiles)
            tile.IsSelected = value && tile.State != SurahState.Unavailable && (where?.Invoke(tile) ?? true);
        _bulkUpdate = false;
        UpdateSummary();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void SelectMissing_Click(object sender, RoutedEventArgs e) => SetAll(true, t => t.State == SurahState.Missing);

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
        var cached = _ctx.Cache.CachedSurahsFor(Id);
        if (cached.Count == 0)
        {
            MessageBox.Show(Strings.Get("dialog.downloadFirst"), Strings.Get("dialog.nothingDownloadedYet"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // Prefer a short surah so the sample is representative but brief.
        var surah = cached.OrderByDescending(s => s).First();
        _ctx.Samples.Toggle(JobKey, _ctx.Cache.AudioPath(Id, surah), (float)_ctx.Settings.Volume);
    }

    private void UpdateListen() =>
        ListenGlyph.Text = _ctx.Samples.PlayingKey == JobKey ? "" : "";

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

    /// <summary>Listed by the source but not actually hosted — cannot be downloaded.</summary>
    Unavailable,
}

internal sealed class ReciterSurahTile : ObservableObject
{
    private static readonly Brush DownloadedBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x7A, 0x63)));
    private static readonly Brush MissingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0x9D, 0x9D)));
    private static readonly Brush DownloadingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00)));
    private static readonly Brush UnavailableBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0x4A, 0x3A)));

    private readonly Action _changed;
    private readonly string _nameArabic;
    private bool _isSelected;
    private bool _isVisible = true;
    private SurahState _state;

    public ReciterSurahTile(Chapter chapter, bool isUntimed, Action changed)
    {
        _changed = changed;
        Number = chapter.Number;
        NameSimple = chapter.NameSimple;
        IsUntimed = isUntimed;
        _nameArabic = chapter.NameArabic;
    }

    public int Number { get; }
    public string NameSimple { get; }
    public SurahState State => _state;

    /// <summary>This recording has the audio but no timings for it: it plays, the panel holds still.</summary>
    public bool IsUntimed { get; }

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
        SurahState.Downloaded => Strings.Get("common.downloaded"),
        SurahState.Downloading => Strings.Get("reciters.downloading"),
        SurahState.Unavailable => Strings.Get("reciters.notAvailable"),
        _ => Strings.Get("reciters.notDownloaded"),
    };

    public string StateGlyph => _state switch
    {
        SurahState.Downloaded => "",   // check mark
        SurahState.Downloading => "",  // download arrow
        SurahState.Unavailable => "",  // warning
        _ => "",
    };

    public Brush StateBrush => _state switch
    {
        SurahState.Downloaded => DownloadedBrush,
        SurahState.Downloading => DownloadingBrush,
        SurahState.Unavailable => UnavailableBrush,
        _ => MissingBrush,
    };

    /// <summary>Unavailable tiles are dimmed and cannot be picked.</summary>
    public bool IsSelectable => _state != SurahState.Unavailable;

    public double TileOpacity => _state == SurahState.Unavailable ? 0.55 : 1.0;

    public void SetState(SurahState state)
    {
        if (_state == state)
            return;
        _state = state;
        Raise(nameof(State));
        Raise(nameof(StateText));
        Raise(nameof(StateGlyph));
        Raise(nameof(StateBrush));
        Raise(nameof(IsSelectable));
        Raise(nameof(TileOpacity));
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
