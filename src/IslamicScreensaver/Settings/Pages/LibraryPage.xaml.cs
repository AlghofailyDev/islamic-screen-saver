using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IslamicScreensaver.Core;
using IslamicScreensaver.Core.Content;
using IslamicScreensaver.Core.Localisation;
using IslamicScreensaver.Core.Settings;
using Microsoft.Win32;

namespace IslamicScreensaver.Settings.Pages;

/// <summary>What is downloaded on this PC: which reciters play, how much space they take, and where they live.</summary>
public partial class LibraryPage : UserControl
{
    private readonly SettingsContext _ctx;
    private readonly Action _browseReciters;
    private readonly List<LibraryRow> _rows = [];
    private bool _moving;

    internal LibraryPage(SettingsContext ctx, Action browseReciters)
    {
        _ctx = ctx;
        _browseReciters = browseReciters;
        InitializeComponent();

        Rotate.IsChecked = _ctx.Settings.RotateReciters;

        _ctx.CatalogChanged += Rebuild;
        _ctx.CacheMoved += Rebuild;
        _ctx.Downloads.Changed += (_, _) => Rebuild();
        _ctx.Samples.PlayingChanged += _ =>
        {
            foreach (var row in _rows)
                row.RefreshPlaying();
        };

        Rebuild();
    }

    private void Rebuild()
    {
        _rows.Clear();
        var selected = _ctx.Settings.Recitations.ToHashSet();

        // One row per downloaded recording, named by its reciter. A recording the catalog no longer
        // lists still appears — the files are on disk, and hiding them would look like data loss.
        foreach (var id in _ctx.Cache.CachedRecitations().OrderBy(r => r.Source).ThenBy(r => r.Number))
            _rows.Add(new LibraryRow(_ctx, id, selected.Contains(id), OnSelectionChanged, Open, Delete));

        List.ItemsSource = _rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        List.Visibility = _rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var surahs = _rows.Sum(r => r.SurahCount);
        SummaryText.Text = _rows.Count == 0
            ? Strings.Get("status.downloadedReciters")
            : Strings.Get("status.recitersSurahs", _rows.Count, surahs);

        UpdateLocation();
    }

    private void UpdateLocation()
    {
        var root = _ctx.Cache.RootDirectory;
        var size = CacheMover.SizeOf(root);
        var isDefault = _ctx.Settings.CacheDirectory is null;
        var suffix = isDefault ? $"  ·  {Strings.Get("status.defaultLocation")}" : string.Empty;
        LocationText.Text = $"{root}  ·  {Strings.Get("common.megabytes", (size / 1024.0 / 1024.0).ToString("0.0"))}{suffix}";
        ResetLocationButton.IsEnabled = !isDefault && !_moving;
        ChangeLocationButton.IsEnabled = !_moving;
    }

    private void OnSelectionChanged()
    {
        // Only the downloaded recordings are listed here, so a selection this page cannot see —
        // something chosen but not yet downloaded — must be carried over rather than dropped.
        var listed = _rows.Select(r => r.Id).ToHashSet();
        var others = _ctx.Settings.Recitations.Where(id => !listed.Contains(id));
        var chosen = _rows.Where(r => r.IsSelected).Select(r => r.Id);

        _ctx.Settings.RecitationIds = [.. chosen.Concat(others).Distinct().Select(id => id.ToString())];
        _ctx.Changed();
    }

    private void Open(LibraryRow row)
    {
        _ctx.Samples.Stop();
        if (_ctx.Catalog.ReciterOf(row.Id) is { } reciter)
            _ctx.NavigateTo?.Invoke(new ReciterDetailPage(_ctx, reciter, Rebuild));
    }

    private void Delete(LibraryRow row)
    {
        var answer = MessageBox.Show(
            Strings.Get("dialog.deleteReciterAsk", row.SurahCount, row.Name),
            Strings.Get("dialog.deleteDownloads"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        _ctx.Samples.Stop();
        foreach (var surah in _ctx.Cache.CachedSurahsFor(row.Id))
            _ctx.Cache.DeleteSurahAudio(row.Id, surah);

        if (_ctx.Settings.Recitations.Contains(row.Id))
            _ctx.Changed();
        Rebuild();
    }

    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && HasButtonAncestor(source, (FrameworkElement)sender))
            return;
        if (sender is FrameworkElement { DataContext: LibraryRow row })
            Open(row);
    }

    private static bool HasButtonAncestor(DependencyObject node, FrameworkElement stopAt)
    {
        while (node is not null && !ReferenceEquals(node, stopAt))
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase)
                return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private void Rotate_Click(object sender, RoutedEventArgs e)
    {
        _ctx.Settings.RotateReciters = Rotate.IsChecked == true;
        _ctx.Changed();
    }

    private void AddReciter_Click(object sender, RoutedEventArgs e) => _browseReciters();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_ctx.Cache.RootDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_ctx.Cache.RootDirectory}\"") { UseShellExecute = true });
    }

    private async void ChangeLocation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Strings.Get("dialog.chooseLocation"),
            InitialDirectory = Directory.Exists(_ctx.Cache.RootDirectory) ? _ctx.Cache.RootDirectory : null,
        };
        if (dialog.ShowDialog() != true)
            return;

        // A folder picker gives the parent; keep our own subfolder so a "Reset" can never delete the user's files.
        var chosen = AppPaths.LibraryInside(dialog.FolderName);
        await MoveLibraryTo(chosen, isDefault: false);
    }

    private async void ResetLocation_Click(object sender, RoutedEventArgs e) =>
        await MoveLibraryTo(AppPaths.CacheDirectory, isDefault: true);

    private async Task MoveLibraryTo(string destination, bool isDefault)
    {
        var source = _ctx.Cache.RootDirectory;
        if (string.Equals(Path.GetFullPath(source).TrimEnd('\\'), Path.GetFullPath(destination).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return;

        if (CacheMover.Validate(source, destination) is { } problem)
        {
            MessageBox.Show(problem, Strings.Get("dialog.cannotUseFolder"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Three distinct answers, and Cancel is not a quiet No. Yes moves the files and repoints the
        // library; No repoints it and leaves the files where they are; Cancel — and the dialog's X,
        // which returns the same — backs out without changing anything at all. Folding Cancel into No
        // meant someone who thought better of it still ended up pointed at an empty folder.
        var move = false;
        var size = CacheMover.SizeOf(source);
        if (size > 0)
        {
            var answer = MessageBox.Show(
                Strings.Get("dialog.moveAsk", (size / 1024.0 / 1024.0).ToString("0.0"), destination),
                Strings.Get("dialog.movePrompt"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);

            if (answer is not (MessageBoxResult.Yes or MessageBoxResult.No))
            {
                Log.Info($"Library move to {destination} cancelled; staying at {source}.");
                return;
            }
            move = answer == MessageBoxResult.Yes;
        }

        _moving = true;
        _ctx.Downloads.CancelAll();
        _ctx.Samples.Stop();
        MovePanel.Visibility = Visibility.Visible;
        UpdateLocation();

        try
        {
            if (move)
            {
                var progress = new Progress<MoveProgress>(p =>
                {
                    MoveProgress.Value = p.Fraction;
                    MoveText.Text = p.CurrentFile is null
                        ? Strings.Get("status.finishing")
                        : Strings.Get("status.movingFile", p.CurrentFile, p.FilesDone, p.FilesTotal);
                });
                MoveText.Text = Strings.Get("status.preparing");
                await CacheMover.MoveAsync(source, destination, progress);
            }

            _ctx.Settings.CacheDirectory = isDefault ? null : destination;
            _ctx.UseCacheAt(destination);
            _ctx.Changed();
            _ctx.Flush();
            _ctx.NotifyCacheMoved();
            Log.Info($"Library location is now {destination}.");
        }
        catch (Exception ex)
        {
            Log.Error($"Could not move the library to {destination}.", ex);
            MessageBox.Show(Strings.Get("dialog.moveFailedBody", ex.Message, source),
                Strings.Get("dialog.moveFailed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _moving = false;
            MovePanel.Visibility = Visibility.Collapsed;
            Rebuild();
        }
    }
}

/// <summary>One downloaded reciter in the library.</summary>
internal sealed class LibraryRow : ObservableObject
{
    private static readonly (string Bg, string Fg)[] AvatarPalette =
    [
        ("#E3ECE8", "#1F5C4B"), ("#ECE8E3", "#6B4A2B"), ("#E3E8EC", "#2B4A6B"),
        ("#E9E3EC", "#5A2B6B"), ("#ECE3E3", "#6B2B2B"), ("#E3ECEA", "#2B6B60"),
    ];

    private readonly SettingsContext _ctx;
    private readonly Action _selectionChanged;
    private bool _isSelected;

    public LibraryRow(SettingsContext ctx, RecitationId id, bool isSelected, Action selectionChanged, Action<LibraryRow> open, Action<LibraryRow> delete)
    {
        _ctx = ctx;
        _selectionChanged = selectionChanged;
        Id = id;
        _isSelected = isSelected;
        JobKey = $"reciter:{id}";

        var recitation = ctx.Recitation(id);
        var reciter = ctx.Catalog.ReciterOf(id);
        Name = reciter?.Name(Strings.IsArabic) ?? id.ToString();
        CanOpen = reciter is not null;

        var palette = AvatarPalette[Math.Abs(id.GetHashCode()) % AvatarPalette.Length];
        AvatarBrush = (Brush)new BrushConverter().ConvertFromString(palette.Bg)!;
        AvatarForeground = (Brush)new BrushConverter().ConvertFromString(palette.Fg)!;
        Initials = Name.Length > 0 ? Name[..1] : "?";

        var surahs = ctx.Cache.CachedSurahsFor(id);
        SurahCount = surahs.Count;
        var style = recitation is null ? "" : $"{recitation.Describe(Strings.IsArabic)}  ·  ";
        Detail = style + Strings.Get("library.surahsOf", SurahCount);
        SizeText = Strings.Get("common.megabytes", (SizeOnDisk(ctx, id, surahs) / 1024.0 / 1024.0).ToString("0.0"));

        OpenCommand = new RelayCommand(() => open(this));
        DeleteCommand = new RelayCommand(() => delete(this));
        PlayCommand = new RelayCommand(Play);
    }

    public RecitationId Id { get; }
    public string Name { get; }

    /// <summary>False for a recording the catalog no longer lists: there is no page to open for it.</summary>
    public bool CanOpen { get; }
    public string Detail { get; }
    public string SizeText { get; }
    public int SurahCount { get; }
    public string Initials { get; }
    public Brush AvatarBrush { get; }
    public Brush AvatarForeground { get; }
    public string JobKey { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand DeleteCommand { get; }
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

    public string PlayGlyph => _ctx.Samples.PlayingKey == JobKey ? "\uE71A" : "\uE768";

    public void RefreshPlaying() => Raise(nameof(PlayGlyph));

    private static long SizeOnDisk(SettingsContext ctx, RecitationId id, IReadOnlyCollection<int> surahs)
    {
        long total = 0;
        foreach (var surah in surahs)
        {
            try
            {
                var file = new FileInfo(ctx.Cache.AudioPath(id, surah));
                if (file.Exists)
                    total += file.Length;
            }
            catch (Exception ex)
            {
                Log.Error($"Could not measure {id}/{surah}.", ex);
            }
        }
        return total;
    }

    private void Play()
    {
        var cached = _ctx.Cache.CachedSurahsFor(Id);
        if (cached.Count == 0)
            return;
        var surah = cached.OrderByDescending(s => s).First();
        _ctx.Samples.Toggle(JobKey, _ctx.Cache.AudioPath(Id, surah), (float)_ctx.Settings.Volume);
    }
}
