using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>
/// One reciter's recordings — a row per riwāyah and style, as the MP3 Quran app lists them. This is
/// where playback selection happens, because "Husary" is not a thing you can play: one of his seven
/// recordings is.
/// </summary>
public partial class ReciterDetailPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly ReciterEntry _reciter;
    private readonly Action _onChanged;
    private readonly List<RecitationRow> _rows = [];

    internal ReciterDetailPage(SettingsContext ctx, ReciterEntry reciter, Action onChanged)
    {
        _ctx = ctx;
        _reciter = reciter;
        _onChanged = onChanged;
        InitializeComponent();

        var name = reciter.Name(Strings.IsArabic);
        Title.Text = name;
        Subtitle.Text = reciter.Recitations.Count == 1
            ? Strings.Get("recitation.one")
            : Strings.Get("recitation.many", reciter.Recitations.Count);
        Initials.Text = name.Length > 0 ? name[..1] : "?";

        foreach (var recitation in reciter.Recitations)
            _rows.Add(new RecitationRow(ctx, reciter, recitation, OnSelectionChanged, RefreshRows));
        List.ItemsSource = _rows;

        _ctx.Downloads.Changed += OnJobChanged;
        Unloaded += (_, _) => _ctx.Downloads.Changed -= OnJobChanged;

        UpdateHint();
    }

    private void OnJobChanged(string key, JobProgress? progress)
    {
        foreach (var row in _rows.Where(r => r.JobKey == key))
            row.OnJob(progress);
        _onChanged();
    }

    private void RefreshRows()
    {
        foreach (var row in _rows)
            row.RefreshStatus();
        _onChanged();
    }

    private void OnSelectionChanged()
    {
        // This reciter's rows replace only this reciter's entries; everyone else's stay as they were.
        var mine = _reciter.Recitations.Select(r => r.Id).ToHashSet();
        var others = _ctx.Settings.Recitations.Where(id => !mine.Contains(id));
        var chosen = _rows.Where(r => r.IsSelected).Select(r => r.Recitation.Id);

        _ctx.Settings.RecitationIds = [.. chosen.Concat(others).Distinct().Select(id => id.ToString())];
        _ctx.Changed();
        UpdateHint();
    }

    private void UpdateHint()
    {
        var chosen = _rows.Count(r => r.IsSelected);
        Hint.Text = chosen == 0 ? Strings.Get("recitation.noneSelected") : string.Empty;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Samples.Stop();
        _onChanged();
        _ctx.NavigateBack?.Invoke();
    }
}

/// <summary>One recording of one reciter: whether it plays, what is downloaded, and how well it syncs.</summary>
internal sealed class RecitationRow : ObservableObject
{
    private static readonly Brush WordBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1F, 0x7A, 0x63)));
    private static readonly Brush VerseBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2B, 0x4A, 0x6B)));
    private static readonly Brush NoneBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x9D, 0x5D, 0x00)));

    private readonly SettingsContext _ctx;
    private readonly Action _selectionChanged;
    private bool _isSelected;
    private string _statusText = string.Empty;
    private bool _hasDownloads;
    private bool _isDownloading;
    private double _progress;

    public RecitationRow(SettingsContext ctx, ReciterEntry reciter, Recitation recitation, Action selectionChanged, Action changed)
    {
        _ctx = ctx;
        _selectionChanged = selectionChanged;
        Recitation = recitation;
        JobKey = $"reciter:{recitation.Id}";
        _isSelected = ctx.Settings.Recitations.Contains(recitation.Id);

        Riwayah = recitation.Riwayah(Strings.IsArabic);
        Style = (Strings.IsArabic ? recitation.StyleArabic : recitation.StyleEnglish ?? recitation.StyleArabic) ?? string.Empty;

        OpenCommand = new RelayCommand(() =>
        {
            ctx.Samples.Stop();
            ctx.NavigateTo?.Invoke(new RecitationSurahsPage(ctx, reciter, recitation, changed));
        });

        OnJob(ctx.Downloads.ProgressOf(JobKey));
        RefreshStatus();
    }

    public Recitation Recitation { get; }
    public string JobKey { get; }
    public string Riwayah { get; }
    public string Style { get; }
    public bool HasStyle => Style.Length > 0;
    public RelayCommand OpenCommand { get; }

    /// <summary>Riwāyah and style keep their own direction — Arabic names inside an English layout and back.</summary>
    public FlowDirection TextDirection =>
        Strings.IsRightToLeftText(Riwayah) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string SourceName => Strings.Get(Recitation.Id.Source == RecitationSource.QuranCom
        ? "recitation.source.qurancom"
        : "recitation.source.mp3quran");

    public string TimingText => Recitation.Timing switch
    {
        TimingLevel.Word => Strings.Get("recitation.timing.word"),
        TimingLevel.Verse when Recitation.TimedSurahs.Count < Recitation.AvailableSurahs.Count =>
            Strings.Get("recitation.timing.partial", Recitation.TimedSurahs.Count, Recitation.AvailableSurahs.Count),
        TimingLevel.Verse => Strings.Get("recitation.timing.verse"),
        _ => Strings.Get("recitation.timing.none"),
    };

    public Brush TimingBrush => Recitation.Timing switch
    {
        TimingLevel.Word => WordBrush,
        TimingLevel.Verse => VerseBrush,
        _ => NoneBrush,
    };

    /// <summary>Shown as a note about the recording. Playback works it out per surah, not from this.</summary>
    public bool OpensWithBasmalah => Recitation.AudioIncludesBasmalah == true;

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
    public bool HasDownloads { get => _hasDownloads; private set => Set(ref _hasDownloads, value); }
    public bool IsDownloading { get => _isDownloading; private set => Set(ref _isDownloading, value); }
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public void RefreshStatus()
    {
        if (IsDownloading)
            return;

        var have = _ctx.Cache.CachedSurahsFor(Recitation.Id).Count;
        var available = Recitation.AvailableSurahs.Count;
        HasDownloads = have > 0;
        StatusText = have == 0
            ? Strings.Get("recitation.surahsAvailable", available)
            : Strings.Get("recitation.downloadedOf", have, available);
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

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
